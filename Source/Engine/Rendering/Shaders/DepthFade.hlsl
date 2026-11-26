// The camera's depth tone and the haze, laid on the lit scene before the upscale and the grade, measured along the view from the
// frame's focus: what stands nearer is burned, as a photographer burns a print in (the near shadow, which takes the direct
// light off it, is Lighting.hlsl's), what lies farther loses its colour, each by its object depth (DepthTone.hlsl) so a whole
// object has one tone, and what lies beyond the haze's start melts into the haze's colour (Haze.hlsl), by its own depth. The haze
// colour is meant for the screen, so it is turned back through the mood's grade (Grade.hlsl). Only where there is paint, by its
// coverage; never the sky. A thread a pixel.

#include "Frame.hlsl"
#include "DepthTone.hlsl"
#include "Grade.hlsl"
#include "Haze.hlsl"

struct DepthFadeConstants
{
    Grading Grade;
    uint Frame;
    uint Output;
};

[[vk::push_constant]] ConstantBuffer<DepthFadeConstants> Pass : register(b0, space0);

// The luminance above which a highlight is held and below half of which a tone is burned fully.
static const float BurnHeldFrom = 0.5;
static const float BurnHeldTo = 2;

// A burn of the given amount: mid-tones sink by their luminance to a power and colours deepen, highlights hold.
float3 Burn(float3 color, float amount)
{
    float luminance = dot(color, ToLuminance);
    float burn = amount * (1 - smoothstep(BurnHeldFrom, BurnHeldTo, luminance));
    float3 sunk = color * pow(saturate(luminance), burn);
    float sunkLuminance = dot(sunk, ToLuminance);
    return max(lerp(sunkLuminance, sunk, 1 + 0.5 * burn), 0);
}

[numthreads(8, 8, 1)]
void ComputeMain(uint3 id : SV_DispatchThreadID)
{
    FrameData frame = LoadFrame(Pass.Frame);
    if (any(id.xy >= uint2(frame.TargetSize)))
    {
        return;
    }

    int3 texel = int3(id.xy, 0);
    float depth = Textures[frame.Depth].Load(texel).r;
    if (depth >= 1)
    {
        return;
    }

    RWTexture2D<float4> output = RWTextures[Pass.Output];
    float4 color = output[id.xy];
    float coverage = saturate(color.a);
    float2 pixel = float2(id.xy) + 0.5;
    float3 world = WorldAtPixel(frame, pixel, depth);
    float objectDepth = Textures[frame.GBufferObjectDepth].Load(texel).r;
    float burn = frame.NearBurn * NearRamp(frame, objectDepth) * coverage;
    float lost = frame.FarAmount * FarRamp(frame, objectDepth) * coverage;
    float3 toned = Burn(color.rgb, burn);
    float grey = dot(toned, ToLuminance);
    toned = lerp(toned, grey, lost);
    float haze = HazeAmount(frame, world) * coverage;
    float3 hazeLight = Ungrade(Pass.Grade, frame.HazeColor);
    output[id.xy] = float4(lerp(toned, hazeLight, haze), color.a);
}
