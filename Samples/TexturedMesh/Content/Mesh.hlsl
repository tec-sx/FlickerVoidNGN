// Draws a mesh whose vertices (float3 position, float3 normal, float2 uv) are pulled from a bindless buffer,
// textured and lit by one directional light given in the mesh's own space.

Texture2D Textures[] : register(t0, space1);
ByteAddressBuffer Buffers[] : register(t0, space2);
SamplerState Samplers[] : register(s0, space3);

struct MeshConstants
{
    row_major float4x4 WorldViewProjection;
    float3 LightDirection;
    uint Vertices;
    uint Texture;
    uint Sampler;
};

[[vk::push_constant]] ConstantBuffer<MeshConstants> Mesh : register(b0, space0);

static const uint VertexStride = 32;
static const float Ambient = 0.35;

struct VertexOutput
{
    float4 Position : SV_Position;
    float3 Normal : NORMAL;
    float2 Uv : TEXCOORD0;
};

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    ByteAddressBuffer vertices = Buffers[Mesh.Vertices];
    uint offset = vertexId * VertexStride;
    float3 position = vertices.Load<float3>(offset);
    float3 normal = vertices.Load<float3>(offset + 12);

    VertexOutput output;
    output.Position = mul(float4(position, 1), Mesh.WorldViewProjection);
    output.Normal = normal;
    output.Uv = vertices.Load<float2>(offset + 24);
    return output;
}

float4 PixelMain(VertexOutput input) : SV_Target
{
    float4 albedo = Textures[Mesh.Texture].Sample(Samplers[Mesh.Sampler], input.Uv);
    float diffuse = saturate(dot(normalize(input.Normal), -normalize(Mesh.LightDirection)));
    return float4(albedo.rgb * (Ambient + (1 - Ambient) * diffuse), 1);
}
