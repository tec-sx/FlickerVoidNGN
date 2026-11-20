// The editor's icons: squares a fixed number of pixels across, centred on world positions and facing the screen, each sampling its
// icon texture and tinting it. Six vertices per icon, read from a bindless buffer of OverlayIconData.

Texture2D Textures[] : register(t0, space1);
ByteAddressBuffer Buffers[] : register(t0, space2);
SamplerState Samplers[] : register(s0, space3);

struct IconConstants
{
    // Multiplies row vectors, as System.Numerics' matrices do.
    row_major float4x4 ViewProjection;
    uint Icons;
    uint Sampler;
    float2 TargetSize;
};

[[vk::push_constant]] ConstantBuffer<IconConstants> Icon : register(b0, space0);

static const uint IconStride = 48;
static const float2 Corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

struct VertexOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 Uv : TEXCOORD0;
    nointerpolation uint Texture : TEXCOORD1;
};

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    ByteAddressBuffer icons = Buffers[Icon.Icons];
    uint offset = (vertexId / 6) * IconStride;
    float3 position = icons.Load<float3>(offset);
    float size = icons.Load<float>(offset + 12);
    float2 corner = Corners[vertexId % 6];

    VertexOutput output;
    float4 clip = mul(float4(position, 1), Icon.ViewProjection);
    clip.xy += corner * (size / Icon.TargetSize) * clip.w;
    output.Position = clip;
    output.Color = icons.Load<float4>(offset + 16);
    output.Texture = icons.Load<uint>(offset + 32);
    output.Uv = float2(corner.x, -corner.y) * 0.5 + 0.5;
    return output;
}

float4 PixelMain(VertexOutput input) : SV_Target
{
    float4 texel = Textures[input.Texture].Sample(Samplers[Icon.Sampler], input.Uv);
    return texel * input.Color;
}
