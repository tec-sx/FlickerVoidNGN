// The scene's acceleration structure, which only devices with ray queries have (set 5, so only shaders created while the frame
// traces rays include this file), and visibility rays through it. A ray passes a cutout draw where its paint is clear, as the
// geometry pass and the shadow maps leave holes there; the scene marks cutout draws not opaque, so their hits come to the shader
// as candidates.

#pragma once

#include "Draw.hlsl"

RaytracingAccelerationStructure AccelerationStructures[] : register(t0, space5);

// Whether the draw's paint covers its triangle at the barycentrics, reading the triangle's corners from the draw's indices and
// vertices.
bool IsPainted(FrameData frame, uint drawIndex, uint triangleIndex, float2 barycentrics)
{
    Draw draw = LoadDraw(frame, drawIndex);
    Material material = LoadMaterial(draw.Material);
    uint3 corners = Buffers[draw.Indices].Load3(triangleIndex * 12);
    ByteAddressBuffer vertices = Buffers[draw.Vertices];
    float2 uv0 = vertices.Load<float2>(corners.x * VertexStride + VertexUv);
    float2 uv1 = vertices.Load<float2>(corners.y * VertexStride + VertexUv);
    float2 uv2 = vertices.Load<float2>(corners.z * VertexStride + VertexUv);
    float2 uv = uv0 * (1 - barycentrics.x - barycentrics.y) + uv1 * barycentrics.x + uv2 * barycentrics.y;
    uint texture = PaintTexture(draw, material, uv);
    // Level 0: a ray has no footprint on the screen to pick a mip with.
    float4 paint = Textures[texture].SampleLevel(Samplers[draw.Sampler], uv, 0);
    return paint.a * draw.BaseColor.a >= material.Cutoff;
}

// 1 when nothing the scene draws blocks the ray from the origin along the unit direction within the distance, else 0.
float TraceVisibility(FrameData frame, float3 origin, float3 direction, float distance)
{
    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = direction;
    ray.TMin = 0;
    ray.TMax = distance;
    RayQuery<RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH> query;
    query.TraceRayInline(AccelerationStructures[frame.AccelerationStructure], RAY_FLAG_NONE, 0xFF, ray);
    while (query.Proceed())
    {
        uint drawIndex = query.CandidateInstanceID();
        uint triangleIndex = query.CandidatePrimitiveIndex();
        float2 barycentrics = query.CandidateTriangleBarycentrics();
        if (IsPainted(frame, drawIndex, triangleIndex, barycentrics))
        {
            query.CommitNonOpaqueTriangleHit();
        }
    }

    if (query.CommittedStatus() == COMMITTED_NOTHING)
    {
        return 1;
    }

    return 0;
}
