// The editor's overlay: coloured triangles in world space, such as gizmos and selection boxes, pulled from a bindless buffer of
// OverlayVertex (float3 position, style, float4 colour) and drawn over everything. A style of 0 is a plain triangle; HatchStyle
// fills it with slanted stripes that keep their width on the screen; anything between is a corner of a line's strip, its distance
// across the line in pixels, a pixel past the line's edge on either side, which fades the edge so thin lines stay smooth.

ByteAddressBuffer Buffers[] : register(t0, space2);

struct OverlayConstants
{
    // Multiplies row vectors, as System.Numerics' matrices do.
    row_major float4x4 ViewProjection;
    uint Vertices;
};

[[vk::push_constant]] ConstantBuffer<OverlayConstants> Overlay : register(b0, space0);

static const uint VertexStride = 32;
static const float HatchStyle = 1000;
// A stripe and the gap after it, in pixels, and how much of the colour's alpha the gap keeps.
static const float HatchPeriod = 9;
static const float HatchStripe = 3;
static const float HatchGap = 0.3;

struct VertexOutput
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
    // The distance across a line in pixels.
    float Across : TEXCOORD0;
    // The style's size, the same at every corner of a triangle.
    nointerpolation float Style : TEXCOORD1;
};

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    ByteAddressBuffer vertices = Buffers[Overlay.Vertices];
    uint offset = vertexId * VertexStride;
    float3 position = vertices.Load<float3>(offset);
    float style = vertices.Load<float>(offset + 12);
    VertexOutput output;
    output.Position = mul(float4(position, 1), Overlay.ViewProjection);
    output.Color = vertices.Load<float4>(offset + 16);
    output.Across = style;
    output.Style = abs(style);
    return output;
}

float4 PixelMain(VertexOutput input) : SV_Target
{
    float4 color = input.Color;
    if (input.Style >= HatchStyle)
    {
        float along = fmod(input.Position.x + input.Position.y, HatchPeriod);
        float stripe = 1 - saturate(abs(along - HatchStripe * 0.5) - HatchStripe * 0.5 + 0.5);
        color.a *= lerp(HatchGap, 1, stripe);
    }
    else if (input.Style > 0)
    {
        float halfWidth = input.Style - 1;
        color.a *= saturate(halfWidth + 0.5 - abs(input.Across));
    }

    return color;
}
