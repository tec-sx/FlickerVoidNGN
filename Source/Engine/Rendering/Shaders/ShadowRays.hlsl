// Traces the shadows of the sun and of up to three lamps (Light.hlsl's RayShadow channels after the sun's), and the ambient
// light's, its occlusion, from every pixel the geometry pass drew, a ray each a frame. TraceMain aims each light's ray at a
// point on the light: within the sun's angle of its direction, or on a disk of the lamp's source radius facing the pixel, so
// shadows sharpen beside their casters and soften away from them; the occlusion's ray leaves over the surface's hemisphere,
// more often near its normal, and counts what it meets within the mood's occlusion radius. The points follow the R2
// low-discrepancy sequence, turned for each pixel and ray by a hash, so a pixel's samples cover the light or the hemisphere
// evenly over the frames; while an upscaler denoises the frame, they are white noise drawn afresh each frame instead, as Ray
// Reconstruction asks for samples without pattern, and the lighting pass reads them unfiltered. FilterMain averages each
// pixel's samples over the frames before, where the history lands on the same surface (by the G-buffer's motion and the view
// depth), up to MaxHistory of them. It first clamps the shadows' history to the spread of this frame's samples round the pixel
// and its own, so a shadow follows its caster at once; the occlusion's isn't, as the few rays that meet something in an open
// corner would be clamped away, so it lags a moving occluder by a few frames. The history keeps the averaged occlusion in red,
// which the lighting pass reads, how many frames it holds in green and the surface's view depth in blue.

#include "Frame.hlsl"
#include "GBuffer.hlsl"
#include "Light.hlsl"
#include "RayTracing.hlsl"

struct ShadowRayConstants
{
    // The view depth axis of the frame before, which the history's depths were measured along.
    float4 PreviousDepthAxis;
    uint Frame;
    uint Raw;
    uint RawOcclusion;
    uint Visibility;
    uint History;
    // None when there is no history to filter with.
    uint PreviousVisibility;
    uint PreviousHistory;
    uint Number;
    // The tangent of half the sun's angle.
    float SunRadius;
    uint Lamp0;
    uint Lamp1;
    uint Lamp2;
};

[[vk::push_constant]] ConstantBuffer<ShadowRayConstants> Pass : register(b0, space0);

static const float Pi = 3.14159265;

// The sample sequence's channel for the occlusion's rays, after the sun's and the lamps'.
static const uint OcclusionChannel = 4;

// How far a ray starts off its surface, along its normal: a little, and more the farther the surface is from the camera, as
// the depth buffer places it less precisely.
static const float RayOffset = 0.005;

static const float RayOffsetPerDepth = 0.001;

static const float MaxHistory = 16;

// How far a surface's view depth may lie from the history's, as a share of it, for the history to count as the same surface.
static const float DepthTolerance = 0.02;

// The pixel's point for the light in this frame, in the unit square: the R2 sequence (Roberts 2018, "The Unreasonable
// Effectiveness of Quasirandom Sequences") in 32-bit fixed point, so it wraps exactly, turned by a hash of the pixel and light;
// or white noise, a hash of the pixel, light and frame, while the frame is denoised.
float2 SamplePoint(FrameData frame, uint2 pixel, uint channel)
{
    uint seed = Hash(pixel.x + Hash(pixel.y + Hash(channel)));
    if (frame.NoiseFrame != None)
    {
        uint first = Hash(seed + Hash(frame.NoiseFrame));
        uint second = Hash(first);
        return float2(HashToUnit(first), HashToUnit(second));
    }

    uint2 rotation = uint2(seed, Hash(seed));
    uint2 fixedPoint = rotation + Pass.Number * uint2(3242174889u, 2447445414u);
    return float2(fixedPoint >> 8) / 16777216.0;
}

// The square's point on the unit disk, keeping its evenness (Shirley and Chiu 1997, "A Low Distortion Map Between Disk and
// Square").
float2 ConcentricDisk(float2 square)
{
    float2 offset = square * 2 - 1;
    if (all(offset == 0))
    {
        return 0;
    }

    float radius;
    float angle;
    if (abs(offset.x) > abs(offset.y))
    {
        radius = offset.x;
        angle = (Pi / 4) * (offset.y / offset.x);
    }
    else
    {
        radius = offset.y;
        angle = (Pi / 2) - (Pi / 4) * (offset.x / offset.y);
    }

    return radius * float2(cos(angle), sin(angle));
}

// The disk's point turned to face along the unit axis (Duff et al. 2017, "Building an Orthonormal Basis, Revisited").
float3 FaceAlong(float3 axis, float2 disk)
{
    float side = axis.z >= 0 ? 1 : -1;
    float a = -1 / (side + axis.z);
    float b = axis.x * axis.y * a;
    float3 tangent = float3(1 + side * axis.x * axis.x * a, side * b, -side * axis.x);
    float3 bitangent = float3(b, side + axis.y * axis.y * a, -axis.y);
    return tangent * disk.x + bitangent * disk.y;
}

float TraceSun(FrameData frame, float3 origin, uint2 pixel)
{
    if (frame.Ambient >= 1)
    {
        return 1;
    }

    float3 toSun = -normalize(frame.SunDirection);
    float2 square = SamplePoint(frame, pixel, ShadowRaySun);
    float2 disk = ConcentricDisk(square) * Pass.SunRadius;
    float3 offset = FaceAlong(toSun, disk);
    float3 direction = normalize(toSun + offset);
    return TraceVisibility(frame, origin, direction, 1e6);
}

// Traces toward a point on the lamp whose place among the lights is given, or lets all light through where there is none or it
// doesn't reach the surface.
float TraceLamp(FrameData frame, uint index, float3 origin, float3 position, uint2 pixel)
{
    if (index == None)
    {
        return 1;
    }

    Light light = LoadLight(frame, index);
    float3 direction;
    float distance;
    float3 reach = LightReach(light, position, direction, distance);
    if (all(reach <= 0))
    {
        return 1;
    }

    float2 square = SamplePoint(frame, pixel, light.RayShadow);
    float2 disk = ConcentricDisk(square) * light.SourceRadius;
    float3 target = light.Position + FaceAlong(direction, disk);
    float3 toTarget = target - origin;
    float span = sqrt(dot(toTarget, toTarget));
    return TraceVisibility(frame, origin, toTarget / span, max(span - LampClearance, 0));
}

// 1 when the ray leaving the surface over its hemisphere meets nothing within the occlusion radius, else 0; 1 without occlusion.
float TraceOcclusion(FrameData frame, float3 origin, float3 normal, uint2 pixel)
{
    if (frame.AmbientOcclusion <= 0 || frame.AmbientOcclusionRadius <= 0)
    {
        return 1;
    }

    // A point even on the disk, lifted onto the hemisphere, leaves along the normal's cosine (Malley's method).
    float2 square = SamplePoint(frame, pixel, OcclusionChannel);
    float2 disk = ConcentricDisk(square);
    float lift = sqrt(saturate(1 - dot(disk, disk)));
    float3 sideways = FaceAlong(normal, disk);
    float3 direction = normalize(sideways + normal * lift);
    return TraceVisibility(frame, origin, direction, frame.AmbientOcclusionRadius);
}

[numthreads(8, 8, 1)]
void TraceMain(uint3 id : SV_DispatchThreadID)
{
    FrameData frame = LoadFrame(Pass.Frame);
    if (any(id.xy >= uint2(frame.TargetSize)))
    {
        return;
    }

    RWTexture2D<float4> raw = RWTextures[Pass.Raw];
    RWTexture2D<float4> rawOcclusion = RWTextures[Pass.RawOcclusion];
    int3 texel = int3(id.xy, 0);
    float depth = Textures[frame.Depth].Load(texel).r;
    if (depth >= 1)
    {
        raw[id.xy] = 1;
        rawOcclusion[id.xy] = 1;
        return;
    }

    float2 pixel = float2(id.xy) + 0.5;
    float3 position = WorldAtPixel(frame, pixel, depth);
    float2 folded = Textures[frame.GBufferNormal].Load(texel).xy;
    float3 normal = DecodeNormal(folded);
    float3 toCamera = ToCamera(frame, position);
    if (dot(normal, toCamera) < 0)
    {
        normal = -normal;
    }

    float viewDepth = dot(float4(position, 1), frame.DepthAxis);
    float3 origin = position + normal * (RayOffset + RayOffsetPerDepth * viewDepth);
    float4 visibility;
    visibility.r = TraceSun(frame, origin, id.xy);
    visibility.g = TraceLamp(frame, Pass.Lamp0, origin, position, id.xy);
    visibility.b = TraceLamp(frame, Pass.Lamp1, origin, position, id.xy);
    visibility.a = TraceLamp(frame, Pass.Lamp2, origin, position, id.xy);
    raw[id.xy] = visibility;
    rawOcclusion[id.xy] = TraceOcclusion(frame, origin, normal, id.xy);
}

// The mean of this frame's shadow samples round the pixel, five by five, and how far they spread from it.
void Neighbourhood(FrameData frame, int2 pixel, out float4 mean, out float4 spread)
{
    Texture2D raw = Textures[Pass.Raw];
    int2 last = int2(frame.TargetSize) - 1;
    float4 sum = 0;
    float4 sumOfSquares = 0;
    for (int y = -2; y <= 2; y++)
    {
        for (int x = -2; x <= 2; x++)
        {
            int2 neighbour = clamp(pixel + int2(x, y), 0, last);
            float4 value = raw.Load(int3(neighbour, 0));
            sum += value;
            sumOfSquares += value * value;
        }
    }

    mean = sum / 25;
    spread = sqrt(max(sumOfSquares / 25 - mean * mean, 0));
}

[numthreads(8, 8, 1)]
void FilterMain(uint3 id : SV_DispatchThreadID)
{
    FrameData frame = LoadFrame(Pass.Frame);
    if (any(id.xy >= uint2(frame.TargetSize)))
    {
        return;
    }

    RWTexture2D<float4> visibility = RWTextures[Pass.Visibility];
    RWTexture2D<float4> history = RWTextures[Pass.History];
    int3 texel = int3(id.xy, 0);
    float depth = Textures[frame.Depth].Load(texel).r;
    if (depth >= 1)
    {
        visibility[id.xy] = 1;
        history[id.xy] = float4(1, 0, 0, 0);
        return;
    }

    float2 pixel = float2(id.xy) + 0.5;
    float3 position = WorldAtPixel(frame, pixel, depth);
    float viewDepth = dot(float4(position, 1), frame.DepthAxis);
    float4 current = Textures[Pass.Raw].Load(texel);
    float currentOcclusion = Textures[Pass.RawOcclusion].Load(texel).r;
    float4 previous = current;
    float previousOcclusion = currentOcclusion;
    float count = 0;
    // The motion is without the jitter, and each frame's samples were moved by its own.
    float2 motion = Textures[frame.GBufferMotion].Load(texel).xy;
    float2 previousPixel = pixel + motion - frame.Jitter + frame.PreviousJitter;
    bool onScreen = all(previousPixel >= 0) && all(previousPixel < frame.TargetSize);
    if (Pass.PreviousHistory != None && onScreen)
    {
        int3 previousTexel = int3(previousPixel, 0);
        float3 kept = Textures[Pass.PreviousHistory].Load(previousTexel).xyz;
        float depthThen = dot(float4(position, 1), Pass.PreviousDepthAxis);
        if (kept.y > 0 && abs(kept.z - depthThen) <= DepthTolerance * depthThen)
        {
            count = kept.y;
            previous = Textures[Pass.PreviousVisibility].Load(previousTexel);
            previousOcclusion = kept.x;
        }
    }

    // The pixel's own sample widens the clamp, else an edge's dark side would be lifted toward its lit neighbours.
    float4 mean;
    float4 spread;
    Neighbourhood(frame, int2(id.xy), mean, spread);
    float4 low = min(mean - spread, current);
    float4 high = max(mean + spread, current);
    float4 clamped = clamp(previous, low, high);
    count = min(count + 1, MaxHistory);
    float weight = 1 / count;
    visibility[id.xy] = lerp(clamped, current, weight);
    float occlusion = lerp(previousOcclusion, currentOcclusion, weight);
    history[id.xy] = float4(occlusion, count, viewDepth, 0);
}
