// The editor's overlay: coloured triangles in world space, such as gizmos and selection boxes, pulled from a bindless buffer of
// OverlayVertex (float3 position, padding, float4 colour) and drawn over everything.

ByteAddressBuffer Buffers[] : register(t0, space2);

struct OverlayConstants
{
    // Multiplies row vectors, as System.Numerics' matrices do.
    row_major float4x4 ViewProjection;
    uint Vertices;
};

[[vk::push_constant]] ConstantBuffer<OverlayConstants> Overlay : register(b0, space0);

static const uint VertexStride = 32;

struct VertexOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    ByteAddressBuffer vertices = Buffers[Overlay.Vertices];
    uint offset = vertexId * VertexStride;
    float3 position = vertices.Load<float3>(offset);
    VertexOutput output;
    output.Position = mul(float4(position, 1), Overlay.ViewProjection);
    output.Color = vertices.Load<float4>(offset + 16);
    return output;
}

float4 PixelMain(VertexOutput input) : SV_Target
{
    return input.Color;
}
