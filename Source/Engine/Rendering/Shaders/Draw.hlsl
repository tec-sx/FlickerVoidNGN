// A draw's record among the frame buffer's draws (GpuDraw.cs), read by the draw's number, the layout of the vertices draws pull
// from bindless buffers (float3 position, float3 normal, float2 uv), and the painting a draw shows.

#pragma once

#include "Frame.hlsl"
#include "Material.hlsl"

// A draw's record: where its mesh stands this frame and the frame before, its vertices then and now, its looks, its outline's
// color, its MeshFlags, its indices, and the centre of its box with how wholly it takes that depth for the depth tone.
struct Draw
{
    float4x4 World;
    float4x4 PreviousWorld;
    float4 BaseColor;
    float4 OutlineColor;
    uint Vertices;
    uint PreviousVertices;
    uint Texture;
    uint Sampler;
    uint Material;
    uint Flags;
    uint Indices;
    float3 Center;
    float Wholeness;
};

static const uint DrawSize = 208;

static const uint VertexStride = 32;

static const uint VertexNormal = 12;

static const uint VertexUv = 24;

// The draw's record; its matrices are the rows of System.Numerics matrices, which multiply row vectors.
Draw LoadDraw(FrameData frame, uint index)
{
    ByteAddressBuffer buffer = Buffers[frame.Buffer];
    uint offset = frame.DrawsOffset + index * DrawSize;
    Draw draw;
    draw.World = float4x4(buffer.Load<float4>(offset), buffer.Load<float4>(offset + 16), buffer.Load<float4>(offset + 32), buffer.Load<float4>(offset + 48));
    draw.PreviousWorld = float4x4(buffer.Load<float4>(offset + 64), buffer.Load<float4>(offset + 80), buffer.Load<float4>(offset + 96), buffer.Load<float4>(offset + 112));
    draw.BaseColor = buffer.Load<float4>(offset + 128);
    draw.OutlineColor = buffer.Load<float4>(offset + 144);
    draw.Vertices = buffer.Load<uint>(offset + 160);
    draw.PreviousVertices = buffer.Load<uint>(offset + 164);
    draw.Texture = buffer.Load<uint>(offset + 168);
    draw.Sampler = buffer.Load<uint>(offset + 172);
    draw.Material = buffer.Load<uint>(offset + 176);
    draw.Flags = buffer.Load<uint>(offset + 180);
    draw.Indices = buffer.Load<uint>(offset + 184);
    draw.Center = buffer.Load<float3>(offset + 192);
    draw.Wholeness = buffer.Load<float>(offset + 204);
    return draw;
}

// The texture the draw is painted with, its material's in place of its own, and the uv to read it at, repeated by the
// material's scale.
uint PaintTexture(Draw draw, Material material, inout float2 uv)
{
    if (material.Texture != None)
    {
        uv *= material.TextureScale;
        return material.Texture;
    }

    return draw.Texture;
}
