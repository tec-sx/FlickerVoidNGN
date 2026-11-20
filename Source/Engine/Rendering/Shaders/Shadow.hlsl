// Draws a mesh part's depth into a shadow atlas tile; vertices are pulled from a bindless buffer as in Scene.hlsl.

ByteAddressBuffer Buffers[] : register(t0, space2);

struct ShadowConstants
{
    row_major float4x4 WorldViewProjection;
    uint Vertices;
};

[[vk::push_constant]] ConstantBuffer<ShadowConstants> Shadow : register(b0, space0);

static const uint VertexStride = 32;

float4 VertexMain(uint vertexId : SV_VertexID) : SV_Position
{
    float3 position = Buffers[Shadow.Vertices].Load<float3>(vertexId * VertexStride);
    return mul(float4(position, 1), Shadow.WorldViewProjection);
}

void PixelMain()
{
}
