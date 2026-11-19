// Draws Dear ImGui's triangles: each vertex (float2 position, float2 uv, uint color) is pulled from a bindless buffer, moved from
// screen pixels to clip space, and the pixel shader multiplies the vertex color by the bound texture.

Texture2D Textures[] : register(t0, space1);
ByteAddressBuffer Buffers[] : register(t0, space2);
SamplerState Samplers[] : register(s0, space3);

struct DebugUiConstants
{
    float2 Scale;
    float2 Translate;
    uint Vertices;
    uint Texture;
    uint Sampler;
    uint LinearOutput;
};

[[vk::push_constant]] ConstantBuffer<DebugUiConstants> Ui : register(b0, space0);

static const uint VertexStride = 20;

struct VertexOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    float2 Uv : TEXCOORD0;
};

float3 SrgbToLinear(float3 color)
{
    float3 low = color / 12.92;
    float3 high = pow((color + 0.055) / 1.055, 2.4);
    return lerp(low, high, step(0.04045, color));
}

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    ByteAddressBuffer vertices = Buffers[Ui.Vertices];
    uint offset = vertexId * VertexStride;
    float2 position = vertices.Load<float2>(offset);
    float2 uv = vertices.Load<float2>(offset + 8);
    uint packed = vertices.Load<uint>(offset + 16);

    float4 color = float4(packed & 255, (packed >> 8) & 255, (packed >> 16) & 255, packed >> 24) / 255.0;
    if (Ui.LinearOutput != 0)
    {
        color.rgb = SrgbToLinear(color.rgb);
    }

    VertexOutput output;
    output.Position = float4((position * Ui.Scale) + Ui.Translate, 0, 1);
    output.Color = color;
    output.Uv = uv;
    return output;
}

float4 PixelMain(VertexOutput input) : SV_Target
{
    return input.Color * Textures[Ui.Texture].Sample(Samplers[Ui.Sampler], input.Uv);
}
