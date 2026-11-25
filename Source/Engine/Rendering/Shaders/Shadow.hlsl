// Draws a mesh part's depth into a shadow atlas tile; vertices are pulled from a bindless buffer as in Scene.hlsl. CutoutMain,
// for parts in a cutout material, reads the paint's coverage through the draw's record and leaves out its holes, where the
// coverage falls below the material's cutoff, so a card casts the shape of its paint.

#include "Draw.hlsl"

struct ShadowConstants
{
    row_major float4x4 WorldViewProjection;
    uint Vertices;
    uint Frame;
    uint Draw;
};

[[vk::push_constant]] ConstantBuffer<ShadowConstants> Shadow : register(b0, space0);

struct ShadowVertex
{
    float4 Position : SV_Position;
    float2 Uv : TEXCOORD0;
};

ShadowVertex VertexMain(uint vertexId : SV_VertexID)
{
    ByteAddressBuffer vertices = Buffers[Shadow.Vertices];
    float3 position = vertices.Load<float3>(vertexId * VertexStride);
    ShadowVertex output;
    output.Position = mul(float4(position, 1), Shadow.WorldViewProjection);
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
