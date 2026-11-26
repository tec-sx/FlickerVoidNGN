// The sky: fills what the scene left empty in the lit scene, before the depth fade, the upscale and the grade, with a gradient
// like an atmosphere: dark ground below the horizon and a lighter warm haze above it fading to a muted blue overhead. A
// perspective view shades by where each pixel's view ray points; an orthographic one, whose rays all point the same way, by
// height on the near plane. Its colours are meant for the screen, so they are turned back through the mood's grade
// (Grade.hlsl), and it is thin paint (coverage 1, no thickness): over a canvas Post.hlsl lets the canvas's weave through it, so
// it is painted at the canvas's average brightness times the gradient over the horizon's, a wash the paper shows through. A
// thread a pixel.

#include "Frame.hlsl"
#include "Grade.hlsl"

struct SkyConstants
{
    Grading Grade;
    float3 CanvasTint;
    uint Canvas;
    uint Frame;
    uint Output;
    uint WrapSampler;
    float Orthographic;
};

[[vk::push_constant]] ConstantBuffer<SkyConstants> Sky : register(b0, space0);

// sRGB colours, turned linear for the screen.
static const float3 Zenith = float3(0.23, 0.33, 0.40);
static const float3 Horizon = float3(0.54, 0.52, 0.44);
static const float3 GroundNear = float3(0.17, 0.16, 0.13);
static const float3 GroundFar = float3(0.07, 0.065, 0.05);

float3 WorldAtNdc(FrameData frame, float2 ndc, float depth)
{
    float4 world = mul(float4(ndc, depth, 1), frame.InverseViewProjection);
    return world.xyz / world.w;
}

// From -1 straight down to 1 straight up, 0 at the horizon.
float Elevation(FrameData frame, float2 pixel)
{
    float3 nearPoint = WorldAtPixel(frame, pixel, 0);
    if (Sky.Orthographic > 0.5)
    {
        float3 top = WorldAtNdc(frame, float2(0, 1), 0);
        float3 bottom = WorldAtNdc(frame, float2(0, -1), 0);
        float3 centre = WorldAtNdc(frame, float2(0, 0), 0);
        return (nearPoint.y - centre.y) / max(length(top - bottom), 0.0001);
    }

    float3 farPoint = WorldAtPixel(frame, pixel, 1);
    float3 direction = normalize(farPoint - nearPoint);
    return direction.y;
}

// The gradient's colour on the screen, linear.
float3 Gradient(float elevation)
{
    float3 color;
    if (elevation >= 0)
    {
        color = lerp(Horizon, Zenith, smoothstep(0, 0.6, elevation));
    }
    else
    {
        color = lerp(GroundNear, GroundFar, smoothstep(0, 0.4, -elevation));
    }

    return pow(color, 2.2);
}

// Over a canvas, the canvas's average brightness (as Post.hlsl weighs it) times the gradient over the horizon's brightness.
float3 Wash(float3 gradient)
{
    Texture2D canvas = Textures[Sky.Canvas];
    uint width;
    uint height;
    uint levels;
    canvas.GetDimensions(0, width, height, levels);
    float3 averageCanvas = canvas.SampleLevel(Samplers[Sky.WrapSampler], 0.5, levels - 1).rgb * Sky.CanvasTint;
    float averageLuminance = max(dot(averageCanvas, ToLuminance), 1e-3);
    float3 horizon = pow(Horizon, 2.2);
    float brightest = dot(horizon, ToLuminance);
    return saturate(gradient / brightest) * averageLuminance;
}

[numthreads(8, 8, 1)]
void ComputeMain(uint3 id : SV_DispatchThreadID)
{
    FrameData frame = LoadFrame(Sky.Frame);
    if (any(id.xy >= uint2(frame.TargetSize)))
    {
        return;
    }

    float depth = Textures[frame.Depth].Load(int3(id.xy, 0)).r;
    if (depth < 1)
    {
        return;
    }

    float2 pixel = float2(id.xy) + 0.5;
    float elevation = Elevation(frame, pixel);
    float3 screen = Gradient(elevation);
    if (Sky.Canvas != None)
    {
        screen = Wash(screen);
    }

    float3 light = Ungrade(Sky.Grade, screen);
    RWTextures[Sky.Output][id.xy] = float4(light, 1);
}
