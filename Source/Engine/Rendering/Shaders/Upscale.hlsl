// Carries the lit scene's alpha (how much paint lies there, plus how thick it stands) over to the scene an upscaler filled in,
// which DLSS leaves without it: each output pixel takes the scene's alpha, filtered between its four nearest pixels, where the
// frame's jitter moved the surface seen there, so Post.hlsl lays the canvas over the upscaled scene as over the scene. A thread
// a pixel.

#include "Frame.hlsl"

struct UpscaleMaskConstants
{
    uint Scene;
    uint Output;
    uint ClampSampler;
    uint Padding;
    // The frame's jitter, in render pixels right and down, and the render size.
    float2 Jitter;
    float2 RenderSize;
};

[[vk::push_constant]] ConstantBuffer<UpscaleMaskConstants> Pass : register(b0, space0);

[numthreads(8, 8, 1)]
void MaskMain(uint3 id : SV_DispatchThreadID)
{
    RWTexture2D<float4> output = RWTextures[Pass.Output];
    uint width;
    uint height;
    output.GetDimensions(width, height);
    if (any(id.xy >= uint2(width, height)))
    {
        return;
    }

    float2 uv = (float2(id.xy) + 0.5) / float2(width, height) + Pass.Jitter / Pass.RenderSize;
    float alpha = Textures[Pass.Scene].SampleLevel(Samplers[Pass.ClampSampler], uv, 0).a;
    float4 color = output[id.xy];
    output[id.xy] = float4(color.rgb, alpha);
}
