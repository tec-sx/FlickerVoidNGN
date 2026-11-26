// The scene's acceleration structure, which only devices with ray queries have (set 5, so only shaders created while the frame
// traces rays include this file), visibility rays through it and rays that find the surface they hit. A ray passes a cutout draw where its paint is clear, as the geometry pass and the shadow maps leave holes
// there; the scene marks cutout draws not opaque, so their hits come to the shader as candidates. Neighbouring rays hit
// different draws, so the indices of a draw's buffers, painting and sampler are marked as varying.

#pragma once

#include "Draw.hlsl"

RaytracingAccelerationStructure AccelerationStructures[] : register(t0, space5);

// How much each of a triangle's three corners counts at the barycentrics a hit gives.
float3 CornerWeights(float2 barycentrics)
{
    return float3(1 - barycentrics.x - barycentrics.y, barycentrics.x, barycentrics.y);
}

// A triangle's corners' vertex attribute (at that offset in a vertex), blended by the corners' weights.
float2 BlendCorners2(ByteAddressBuffer vertices, uint3 corners, uint attribute, float3 weights)
{
    float2 first = vertices.Load<float2>(corners.x * VertexStride + attribute);
    float2 second = vertices.Load<float2>(corners.y * VertexStride + attribute);
    float2 third = vertices.Load<float2>(corners.z * VertexStride + attribute);
    return first * weights.x + second * weights.y + third * weights.z;
}

float3 BlendCorners3(ByteAddressBuffer vertices, uint3 corners, uint attribute, float3 weights)
{
    float3 first = vertices.Load<float3>(corners.x * VertexStride + attribute);
    float3 second = vertices.Load<float3>(corners.y * VertexStride + attribute);
    float3 third = vertices.Load<float3>(corners.z * VertexStride + attribute);
    return first * weights.x + second * weights.y + third * weights.z;
}

// The paint of the draw at the uv: its painting's, or its material's in place of it, times its base color.
float4 SamplePaint(Draw draw, Material material, float2 uv)
{
    uint texture = PaintTexture(draw, material, uv);
    Texture2D painting = Textures[NonUniformResourceIndex(texture)];
    SamplerState sampler = Samplers[NonUniformResourceIndex(draw.Sampler)];
    // Level 0: a ray has no footprint on the screen to pick a mip with.
    float4 paint = painting.SampleLevel(sampler, uv, 0);
    return paint * draw.BaseColor;
}

// Whether the draw's paint covers its triangle at the barycentrics, reading the triangle's corners from the draw's indices and
// vertices.
bool IsPainted(FrameData frame, uint drawIndex, uint triangleIndex, float2 barycentrics)
{
    Draw draw = LoadDraw(frame, drawIndex);
    Material material = LoadMaterial(draw.Material);
    uint3 corners = Buffers[NonUniformResourceIndex(draw.Indices)].Load3(triangleIndex * 12);
    float3 weights = CornerWeights(barycentrics);
    float2 uv = BlendCorners2(Buffers[NonUniformResourceIndex(draw.Vertices)], corners, VertexUv, weights);
    float4 paint = SamplePaint(draw, material, uv);
    return paint.a >= material.Cutoff;
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

// Where a ray stopped: whether it hit anything, how far along it, and the draw and triangle it hit and where on it.
struct RayHit
{
    bool Hit;
    float Distance;
    uint Draw;
    uint Triangle;
    float2 Barycentrics;
};

// The first surface the scene draws along the ray from the origin along the unit direction within the distance.
RayHit TraceClosest(FrameData frame, float3 origin, float3 direction, float distance)
{
    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = direction;
    ray.TMin = 0;
    ray.TMax = distance;
    RayQuery<RAY_FLAG_NONE> query;
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

    RayHit hit = (RayHit)0;
    if (query.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
    {
        hit.Hit = true;
        hit.Distance = query.CommittedRayT();
        hit.Draw = query.CommittedInstanceID();
        hit.Triangle = query.CommittedPrimitiveIndex();
        hit.Barycentrics = query.CommittedTriangleBarycentrics();
    }

    return hit;
}

// The surface a ray hit, as the geometry pass would leave it: where it is, its normal, its color and its material's index.
struct HitSurface
{
    float3 Position;
    float3 Normal;
    float3 Albedo;
    uint Material;
};

// The surface the ray from the origin along the unit direction hit: its normal blended from its corners' and turned as its draw
// stands, as the geometry pass turns it, and its paint times its material's tint.
HitSurface LoadHitSurface(FrameData frame, RayHit hit, float3 origin, float3 direction)
{
    Draw draw = LoadDraw(frame, hit.Draw);
    Material material = LoadMaterial(draw.Material);
    uint3 corners = Buffers[NonUniformResourceIndex(draw.Indices)].Load3(hit.Triangle * 12);
    ByteAddressBuffer vertices = Buffers[NonUniformResourceIndex(draw.Vertices)];
    float3 weights = CornerWeights(hit.Barycentrics);
    float3 normal = BlendCorners3(vertices, corners, VertexNormal, weights);
    float2 uv = BlendCorners2(vertices, corners, VertexUv, weights);
    float3x3 basis = (float3x3)draw.World;
    float3x3 cofactor = float3x3(cross(basis[1], basis[2]), cross(basis[2], basis[0]), cross(basis[0], basis[1]));
    float mirror = sign(dot(basis[0], cofactor[0]));
    float3 turned = mul(normal, cofactor) * mirror;
    float4 paint = SamplePaint(draw, material, uv);

    HitSurface surface;
    surface.Position = origin + direction * hit.Distance;
    surface.Normal = normalize(turned);
    surface.Albedo = paint.rgb * material.Tint;
    surface.Material = draw.Material;
    return surface;
}
