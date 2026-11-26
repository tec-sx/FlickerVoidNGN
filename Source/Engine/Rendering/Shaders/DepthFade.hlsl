// The camera's depth fade and the haze, laid on the lit scene before the upscale and the grade, measured along the view from the
// frame's focus: what stands nearer darkens, as if a shadow fell on it, what lies farther melts into the far colour, and what
// lies beyond the haze's start melts into the haze's (Haze.hlsl). The far and haze colours are meant for the screen, so they
// are turned back through the mood's grade (Grade.hlsl). Only where there is paint, by its coverage; never the sky. A thread a
// pixel.

#include "Frame.hlsl"
#include "Grade.hlsl"
#include "Haze.hlsl"

struct DepthFadeConstants
{
    Grading Grade;
    uint Frame;
    uint Output;
};

[[vk::push_constant]] ConstantBuffer<DepthFadeConstants> Pass : register(b0, space0);

// 0 at or before start, 1 at or past end, eased between.
float Ramp(float start, float end, float value)
{
    float amount = saturate((value - start) / max(end - start, 1e-4));
    return amount * amount * (3 - 2 * amount);
}

[numthreads(8, 8, 1)]
void ComputeMain(uint3 id : SV_DispatchThreadID)
{
    FrameData frame = LoadFrame(Pass.Frame);
    if (any(id.xy >= uint2(frame.TargetSize)))
    {
        return;
    }

    float depth = Textures[frame.Depth].Load(int3(id.xy, 0)).r;
    if (depth >= 1)
    {
        return;
    }

    RWTexture2D<float4> output = RWTextures[Pass.Output];
    float4 color = output[id.xy];
    float coverage = saturate(color.a);
    float2 pixel = float2(id.xy) + 0.5;
    float3 world = WorldAtPixel(frame, pixel, depth);
    float beyondFocus = dot(float4(world, 1), frame.DepthAxis) - frame.FocusDepth;
    float near = frame.NearAmount * Ramp(frame.NearStart, frame.NearEnd, -beyondFocus) * coverage;
    float far = frame.FarAmount * Ramp(frame.FarStart, frame.FarEnd, beyondFocus) * coverage;
    float3 faded = color.rgb * (1 - near);
    float3 farLight = Ungrade(Pass.Grade, frame.FarColor);
    float3 melted = lerp(faded, farLight, far);
    float haze = HazeAmount(frame, world) * coverage;
    float3 hazeLight = Ungrade(Pass.Grade, frame.HazeColor);
    output[id.xy] = float4(lerp(melted, hazeLight, haze), color.a);
}
