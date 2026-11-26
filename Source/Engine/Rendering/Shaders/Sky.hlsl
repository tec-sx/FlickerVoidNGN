// The sky: fills what the scene left empty in the lit scene, before the depth fade, the upscale and the grade. Its gradient is
// the mood's: the ground below the horizon (darkening farther down), the horizon's colour and the zenith's overhead, blended
// across the horizon over its softness. A perspective view shades by where each pixel's view ray points; an orthographic one,
// whose rays all point the same way, by height on the near plane. The gradient's colours are meant for the screen, so they are
// turned back through the mood's grade (Grade.hlsl), and it is thin paint (coverage 1, no thickness): over a canvas Post.hlsl
// lets the canvas's weave through it, so it is painted at the canvas's average brightness times the gradient over the horizon's,
// a wash the paper shows through. Over it, as light that the grade, bloom and flares take as they take the scene's, the sun's
// disc and glow and the moon's disc, lit on the side facing the sun, and its corona; the ground hides them below the horizon.
// A thread a pixel.

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
    uint Data;
};

[[vk::push_constant]] ConstantBuffer<SkyConstants> Sky : register(b0, space0);

// Mirrors GpuSky.
struct SkyData
{
    float3 Zenith;
    float HorizonSoftness;
    float3 Horizon;
    float SunDiscCosine;
    float3 Ground;
    float SunDiscIntensity;
    float3 TowardSun;
    float SunGlow;
    float3 SunColor;
    float SunGlowRadians;
    float3 TowardMoon;
    float MoonDiscCosine;
    float3 MoonColor;
    float MoonDiscIntensity;
    float MoonCorona;
    float MoonDiscRadians;
    float Padding0;
    float Padding1;
};

// How much darker the ground is straight down than at the horizon.
static const float GroundFalloff = 0.14;

// The width of a disc's soft rim, as a share of its radius.
static const float DiscRim = 0.15;

SkyData LoadSky()
{
    return Buffers[Sky.Data].Load<SkyData>(0);
}

float3 WorldAtNdc(FrameData frame, float2 ndc, float depth)
{
    float4 world = mul(float4(ndc, depth, 1), frame.InverseViewProjection);
    return world.xyz / world.w;
}

// The direction a perspective pixel looks along.
float3 ViewRay(FrameData frame, float2 pixel)
{
    float3 nearPoint = WorldAtPixel(frame, pixel, 0);
    float3 farPoint = WorldAtPixel(frame, pixel, 1);
    return normalize(farPoint - nearPoint);
}

// From -1 straight down to 1 straight up, 0 at the horizon.
float Elevation(FrameData frame, float2 pixel)
{
    if (Sky.Orthographic > 0.5)
    {
        float3 nearPoint = WorldAtPixel(frame, pixel, 0);
        float3 top = WorldAtNdc(frame, float2(0, 1), 0);
        float3 bottom = WorldAtNdc(frame, float2(0, -1), 0);
        float3 centre = WorldAtNdc(frame, float2(0, 0), 0);
        return (nearPoint.y - centre.y) / max(length(top - bottom), 0.0001);
    }

    return ViewRay(frame, pixel).y;
}

// How much of the sky, rather than the ground, shows at an elevation.
float SkyShare(SkyData sky, float elevation)
{
    float softness = max(sky.HorizonSoftness, 1e-4);
    return smoothstep(-softness, softness, elevation);
}

// The gradient's colour on the screen, linear.
float3 Gradient(SkyData sky, float elevation)
{
    float3 above = lerp(sky.Horizon, sky.Zenith, smoothstep(0, 0.6, max(elevation, 0)));
    float3 below = lerp(sky.Ground, sky.Ground * GroundFalloff, smoothstep(0, 0.4, max(-elevation, 0)));
    return lerp(below, above, SkyShare(sky, elevation));
}

// Over a canvas, the canvas's average brightness (as Post.hlsl weighs it) times the gradient over the horizon's brightness.
float3 Wash(SkyData sky, float3 gradient)
{
    Texture2D canvas = Textures[Sky.Canvas];
    uint width;
    uint height;
    uint levels;
    canvas.GetDimensions(0, width, height, levels);
    float3 averageCanvas = canvas.SampleLevel(Samplers[Sky.WrapSampler], 0.5, levels - 1).rgb * Sky.CanvasTint;
    float averageLuminance = max(dot(averageCanvas, ToLuminance), 1e-3);
    float brightest = max(dot(sky.Horizon, ToLuminance), 1e-4);
    return saturate(gradient / brightest) * averageLuminance;
}

// 1 inside a disc of the cosine of its radius, falling to 0 over a soft rim.
float Disc(float cosine, float discCosine)
{
    float radius = acos(clamp(discCosine, -1, 1));
    float angle = acos(clamp(cosine, -1, 1));
    return 1 - smoothstep(radius * (1 - DiscRim), radius, angle);
}

// The sun's disc and glow, as light.
float3 SunLight(SkyData sky, float3 direction)
{
    float cosine = dot(direction, sky.TowardSun);
    float disc = Disc(cosine, sky.SunDiscCosine) * sky.SunDiscIntensity;
    float angle = acos(clamp(cosine, -1, 1));
    float reach = saturate(1 - angle / max(sky.SunGlowRadians, 1e-4));
    float glow = sky.SunGlow * reach * reach;
    return sky.SunColor * (disc + glow);
}

// The moon's disc, lit where its face turns toward the sun as a ball is, and its corona, as bright as its lit share.
float3 MoonLight(SkyData sky, float3 direction)
{
    float3 centre = sky.TowardMoon;
    float cosine = dot(direction, centre);
    float lit = (1 - dot(sky.TowardSun, centre)) * 0.5;
    float3 light = 0;
    float disc = Disc(cosine, sky.MoonDiscCosine);
    if (disc > 0 && cosine > 0)
    {
        float radius = max(sin(sky.MoonDiscRadians), 1e-5);
        float3 across = (direction - centre * cosine) / radius;
        float facing = sqrt(saturate(1 - dot(across, across)));
        float3 normal = across - centre * facing;
        float shade = saturate(dot(normal, sky.TowardSun));
        light += sky.MoonColor * (disc * shade * sky.MoonDiscIntensity);
    }

    float angle = acos(clamp(cosine, -1, 1));
    float reach = saturate(1 - angle / max(sky.MoonDiscRadians * 6, 1e-4));
    light += sky.MoonColor * (sky.MoonCorona * lit * reach * reach);
    return light;
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

    SkyData sky = LoadSky();
    float2 pixel = float2(id.xy) + 0.5;
    float elevation = Elevation(frame, pixel);
    float3 screen = Gradient(sky, elevation);
    if (Sky.Canvas != None)
    {
        screen = Wash(sky, screen);
    }

    float3 light = Ungrade(Sky.Grade, screen);
    if (Sky.Orthographic < 0.5)
    {
        float3 direction = ViewRay(frame, pixel);
        float3 bodies = SunLight(sky, direction) + MoonLight(sky, direction);
        light += bodies * SkyShare(sky, elevation);
    }

    RWTextures[Sky.Output][id.xy] = float4(light, 1);
}
