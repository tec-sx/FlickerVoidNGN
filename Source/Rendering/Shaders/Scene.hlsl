// Draws a mesh part whose vertices (float3 position, float3 normal, float2 uv) are pulled from a bindless buffer:
// its base color times its texture, lit by the sun given in the part's own space and by an even ambient light.

Texture2D Textures[] : register(t0, space1);
ByteAddressBuffer Buffers[] : register(t0, space2);
SamplerState Samplers[] : register(s0, space3);

struct SceneConstants
{
    row_major float4x4 WorldViewProjection;
    float3 SunDirection;
    uint Vertices;
    uint Texture;
    uint Sampler;
    float Ambient;
    uint Padding;
    float4 BaseColor;
};

[[vk::push_constant]] ConstantBuffer<SceneConstants> Scene : register(b0, space0);

static const uint VertexStride = 32;

struct VertexOutput
{
    float4 Position : SV_Position;
    float3 Normal : NORMAL;
    float2 Uv : TEXCOORD0;
};

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    ByteAddressBuffer vertices = Buffers[Scene.Vertices];
    uint offset = vertexId * VertexStride;
    float3 position = vertices.Load<float3>(offset);

    VertexOutput output;
    output.Position = mul(float4(position, 1), Scene.WorldViewProjection);
    output.Normal = vertices.Load<float3>(offset + 12);
    output.Uv = vertices.Load<float2>(offset + 24);
    return output;
}

float4 PixelMain(VertexOutput input) : SV_Target
{
    float4 albedo = Textures[Scene.Texture].Sample(Samplers[Scene.Sampler], input.Uv) * Scene.BaseColor;
    float sun = saturate(dot(normalize(input.Normal), -normalize(Scene.SunDirection)));
    float light = Scene.Ambient + (1 - Scene.Ambient) * sun;
    return float4(albedo.rgb * light, albedo.a);
}
