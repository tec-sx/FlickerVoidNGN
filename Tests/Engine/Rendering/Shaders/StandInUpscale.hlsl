// Stands in for DLSS in UpscalePassTests: fills each output pixel with the nearest pixel of the smaller, jittered frame where
// the jitter moved the surface seen there, with alpha 1, as DLSS leaves the output's alpha. A thread a pixel.

Texture2D Textures[] : register(t0, space1);
RWTexture2D<float4> RWTextures[] : register(u0, space4);

struct StandInUpscaleConstants
{
    uint Color;
    uint Output;
    // Render pixels to an output pixel, across and down.
    float2 Scale;
    float2 Jitter;
    float2 RenderSize;
};

[[vk::push_constant]] ConstantBuffer<StandInUpscaleConstants> Pass : register(b0, space0);

[numthreads(8, 8, 1)]
void ComputeMain(uint3 id : SV_DispatchThreadID)
{
    RWTexture2D<float4> output = RWTextures[Pass.Output];
    uint width;
    uint height;
    output.GetDimensions(width, height);
    if (any(id.xy >= uint2(width, height)))
    {
        return;
    }

    float2 drawn = (float2(id.xy) + 0.5) * Pass.Scale + Pass.Jitter;
    int2 texel = clamp(int2(floor(drawn)), 0, int2(Pass.RenderSize) - 1);
    float3 color = Textures[Pass.Color].Load(int3(texel, 0)).rgb;
    output[id.xy] = float4(color, 1);
}
