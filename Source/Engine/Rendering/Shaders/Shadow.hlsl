// Draws a mesh part's depth into a shadow atlas tile; vertices are pulled from a bindless buffer as in Scene.hlsl. CutoutMain,
// for parts in a cutout material, reads the paint's coverage through the draw's record and leaves out its holes, where the
// coverage falls below the material's cutoff, so a card casts the shape of its paint. A batch's part draws every copy, an
// instance each, placed as the copy says (Instance.hlsl), its draw's world being the identity.

#include "Draw.hlsl"
#include "Instance.hlsl"

struct ShadowConstants
{
    row_major float4x4 WorldViewProjection;
    uint Vertices;
    uint Frame;
    uint Draw;
    // A batch's copies, else None.
    uint Copies;
};

[[vk::push_constant]] ConstantBuffer<ShadowConstants> Shadow : register(b0, space0);

struct ShadowVertex
{
    float4 Position : SV_Position;
    float2 Uv : TEXCOORD0;
};

ShadowVertex VertexMain(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
{
    ByteAddressBuffer vertices = Buffers[Shadow.Vertices];
    float4 position = float4(vertices.Load<float3>(vertexId * VertexStride), 1);
    if (Shadow.Copies != None)
    {
        FrameData frame = LoadFrame(Shadow.Frame);
        Draw draw = LoadDraw(frame, Shadow.Draw);
        Instance copy = LoadInstance(Buffers[Shadow.Copies], instanceId);
        float size = InstanceScale(copy, draw.Scales);
        float4x4 placement = InstanceWorld(copy, size);
        position = mul(position, placement);
    }

    ShadowVertex output;
    output.Position = mul(position, Shadow.WorldViewProjection);
    output.Uv = vertices.Load<float2>(vertexId * VertexStride + VertexUv);
    return output;
}

void PixelMain()
{
}

void CutoutMain(ShadowVertex input)
{
    FrameData frame = LoadFrame(Shadow.Frame);
    Draw draw = LoadDraw(frame, Shadow.Draw);
    Material material = LoadMaterial(draw.Material);
    float2 uv = input.Uv;
    uint texture = PaintTexture(draw, material, uv);
    float4 paint = Textures[texture].Sample(Samplers[draw.Sampler], uv);
    if (paint.a * draw.BaseColor.a < material.Cutoff)
    {
        discard;
    }
}
