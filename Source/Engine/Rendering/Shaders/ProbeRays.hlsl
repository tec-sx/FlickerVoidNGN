// Traces the irradiance volume's probes live, as dynamic diffuse global illumination does (Majercik et al. 2019, "Dynamic Diffuse
// Global Illumination with Ray-Traced Irradiance Fields"). TraceMain sends each probe's rays over the sphere, spread evenly (a
// spherical Fibonacci set) and turned anew each frame, and finds the light each meets: the surface it hits lit by the sun, the
// ambient light, the lamps and the probes themselves, so light bounces on from frame to frame, plus the surface's glow; nothing
// from a surface's back or where it meets nothing, as the ambient light stands for the sky. UpdateMain folds a probe's rays
// into its light and its map of distances (Probes.hlsl), keeping the hysteresis's share of what they held.
// A ray's record in the rays' buffer: its direction, how far it went (the probe's reach where it met nothing within it), then the
// light it met.

#include "Frame.hlsl"
#include "Light.hlsl"
#include "Material.hlsl"
#include "Probes.hlsl"
#include "RayTracing.hlsl"

RWByteAddressBuffer RWBuffers[] : register(u0, space2);

struct ProbeRayConstants
{
    uint Frame;
    uint Rays;
    uint Probes;
    uint RayCount;
    uint Number;
    float Hysteresis;
};

[[vk::push_constant]] ConstantBuffer<ProbeRayConstants> Pass : register(b0, space0);

static const float Pi = 3.14159265;

static const uint RaySize = 32;

static const uint GroupSize = 64;

// How far a probe's rays look for a surface.
static const float RayLength = 1000;

// How far rays leaving a hit surface toward the lights start off it, along its normal.
static const float HitOffset = 0.01;

// How sharply a texel of a probe's map of distances favours the rays nearest its direction.
static const float DistanceSharpness = 50;

// The ray's direction, one of the count spread evenly over the sphere (Keinert et al. 2015, "Spherical Fibonacci Mapping").
float3 SphericalFibonacci(uint index, uint count)
{
    float goldenShare = (sqrt(5.0) - 1) / 2;
    float turn = 2 * Pi * frac(index * goldenShare);
    float height = 1 - (2 * index + 1) / float(count);
    float radius = sqrt(saturate(1 - height * height));
    return float3(cos(turn) * radius, sin(turn) * radius, height);
}

// A share of the unit range from a hash's 24 high bits.
float UnitShare(uint hash)
{
    return float(hash >> 8) / 16777216.0;
}

// The frame's turn of every probe's rays, a rotation picked evenly among all rotations as a quaternion (Shoemake 1992, "Uniform
// Random Rotations"), so the rays cover the sphere over the frames.
float4 FrameRotation(uint number)
{
    uint first = Hash(number);
    uint second = Hash(first);
    uint third = Hash(second);
    float share = UnitShare(first);
    float angle = 2 * Pi * UnitShare(second);
    float otherAngle = 2 * Pi * UnitShare(third);
    float outer = sqrt(1 - share);
    float inner = sqrt(share);
    return float4(outer * sin(angle), outer * cos(angle), inner * sin(otherAngle), inner * cos(otherAngle));
}

float3 Rotate(float4 rotation, float3 vector)
{
    float3 twice = 2 * cross(rotation.xyz, vector);
    return vector + rotation.w * twice + cross(rotation.xyz, twice);
}

// The sun's light on the surface, where a ray toward it meets nothing.
float3 SunOn(FrameData frame, Material material, float3 origin, float3 normal)
{
    float3 toSun = -normalize(frame.SunDirection);
    float sun = (1 - frame.Ambient) * Diffuse(material, dot(normal, toSun));
    if (sun <= 0 || all(frame.SunLight <= 0))
    {
        return 0;
    }

    return sun * frame.SunLight * TraceVisibility(frame, origin, toSun, 1e6);
}

// The lamps' light on the surface; a lamp that casts shadows lights it where a ray toward it meets nothing.
float3 LampsOn(FrameData frame, Material material, float3 position, float3 origin, float3 normal)
{
    float3 light = 0;
    for (uint index = 0; index < frame.LightCount; index++)
    {
        Light lamp = LoadLight(frame, index);
        float3 direction;
        float distance;
        float3 reach = LightReach(lamp, position, direction, distance);
        float diffuse = Diffuse(material, dot(normal, direction));
        if (all(reach <= 0) || diffuse <= 0)
        {
            continue;
        }

        if (lamp.Cookie != None)
        {
            reach *= SampleCookie(frame, lamp, -direction);
        }

        if (lamp.Shadow != None || lamp.RayShadow != None)
        {
            float3 toLamp = lamp.Position - origin;
            float span = length(toLamp);
            reach *= TraceVisibility(frame, origin, toLamp / max(span, 1e-4), max(span - LampClearance, 0));
        }

        light += reach * diffuse;
    }

    return light;
}

// The light leaving the surface the ray hit back along it: its color lit as the lighting pass lights it, but for the painted
// touches that depend on the view and the ambient occlusion, plus its glow; nothing from a surface's back.
float3 ShadeHit(FrameData frame, RayHit hit, float3 origin, float3 direction)
{
    HitSurface surface = LoadHitSurface(frame, hit, origin, direction);
    if (dot(surface.Normal, direction) > 0)
    {
        return 0;
    }

    Material material = LoadMaterial(surface.Material);
    float3 lightOrigin = surface.Position + surface.Normal * HitOffset;
    float3 light = frame.Ambient * IndoorAmbient(frame, surface.Position);
    light += SampleProbes(frame, surface.Position, surface.Normal, -direction);
    light += SunOn(frame, material, lightOrigin, surface.Normal);
    light += LampsOn(frame, material, surface.Position, lightOrigin, surface.Normal);
    float3 shaded = ShadeSurface(material, surface.Albedo, light, 0, light);
    return shaded + material.Emissive * material.EmissiveIntensity;
}

[numthreads(GroupSize, 1, 1)]
void TraceMain(uint3 id : SV_DispatchThreadID)
{
    uint ray = id.x;
    uint probe = id.y;
    if (ray >= Pass.RayCount)
    {
        return;
    }

    FrameData frame = LoadFrame(Pass.Frame);
    uint3 cell = ProbeCell(frame, probe);
    float3 origin = ProbePosition(frame, cell);
    float4 rotation = FrameRotation(Pass.Number);
    float3 spread = SphericalFibonacci(ray, Pass.RayCount);
    float3 direction = Rotate(rotation, spread);
    float reach = ProbeReach(frame);
    RayHit hit = TraceClosest(frame, origin, direction, RayLength);
    float distance = reach;
    float3 radiance = 0;
    if (hit.Hit)
    {
        distance = min(hit.Distance, reach);
        radiance = ShadeHit(frame, hit, origin, direction);
    }

    RWByteAddressBuffer rays = RWBuffers[Pass.Rays];
    uint offset = (probe * Pass.RayCount + ray) * RaySize;
    rays.Store<float3>(offset, direction);
    rays.Store<float>(offset + 12, distance);
    rays.Store<float3>(offset + 16, radiance);
}

// Folds the probe's rays into its light. Rays spread evenly over the sphere each stand for its share of it, so the convolved
// constant term (over pi) is their mean light and the linear terms twice the mean of their light times their direction.
void UpdateLight(RWByteAddressBuffer probes, ByteAddressBuffer rays, uint probe, uint first)
{
    float4 red = 0;
    float4 green = 0;
    float4 blue = 0;
    for (uint ray = 0; ray < Pass.RayCount; ray++)
    {
        uint offset = first + ray * RaySize;
        float3 direction = rays.Load<float3>(offset);
        float3 radiance = rays.Load<float3>(offset + 16);
        float4 basis = float4(1, 2 * direction);
        red += radiance.r * basis;
        green += radiance.g * basis;
        blue += radiance.b * basis;
    }

    float share = 1.0 / Pass.RayCount;
    uint probeOffset = probe * ProbeSize;
    float4 keptRed = probes.Load<float4>(probeOffset);
    float4 keptGreen = probes.Load<float4>(probeOffset + 16);
    float4 keptBlue = probes.Load<float4>(probeOffset + 32);
    probes.Store<float4>(probeOffset, lerp(red * share, keptRed, Pass.Hysteresis));
    probes.Store<float4>(probeOffset + 16, lerp(green * share, keptGreen, Pass.Hysteresis));
    probes.Store<float4>(probeOffset + 32, lerp(blue * share, keptBlue, Pass.Hysteresis));
}

// Folds the probe's rays into a texel of its map of distances, weighing each by how near its direction is to the texel's; a texel
// no ray came near keeps what it held.
void UpdateDistance(FrameData frame, RWByteAddressBuffer probes, ByteAddressBuffer rays, uint probe, uint2 texel, uint first)
{
    float3 texelDirection = DistanceTexelDirection(texel);
    float reach = ProbeReach(frame);
    float2 moments = 0;
    float total = 0;
    for (uint ray = 0; ray < Pass.RayCount; ray++)
    {
        uint offset = first + ray * RaySize;
        float3 direction = rays.Load<float3>(offset);
        float weight = pow(saturate(dot(texelDirection, direction)), DistanceSharpness);
        if (weight <= 0)
        {
            continue;
        }

        float distance = rays.Load<float>(offset + 12) / reach;
        moments += weight * float2(distance, distance * distance);
        total += weight;
    }

    if (total <= 0)
    {
        return;
    }

    uint distanceOffset = DistanceOffset(frame, probe, texel);
    float2 kept = probes.Load<float2>(distanceOffset);
    probes.Store<float2>(distanceOffset, lerp(moments / total, kept, Pass.Hysteresis));
}

// A group a probe, a thread a texel of its map of distances; the first also folds its light.
[numthreads(DistanceSize, DistanceSize, 1)]
void UpdateMain(uint3 group : SV_GroupID, uint3 thread : SV_GroupThreadID)
{
    FrameData frame = LoadFrame(Pass.Frame);
    uint probe = group.x;
    ByteAddressBuffer rays = Buffers[Pass.Rays];
    RWByteAddressBuffer probes = RWBuffers[Pass.Probes];
    uint first = probe * Pass.RayCount * RaySize;
    if (all(thread.xy == 0))
    {
        UpdateLight(probes, rays, probe, first);
    }

    UpdateDistance(frame, probes, rays, probe, thread.xy, first);
}
