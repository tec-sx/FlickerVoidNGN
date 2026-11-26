// Draws a mesh part whose vertices (float3 position, float3 normal, float2 uv) are pulled from a bindless buffer, as its record
// among the frame buffer's draws says (pushed by its number). The geometry pass leaves its surface in the G-buffer (GBuffer.cs)
// for Lighting.hlsl: its base color times its texture and its material's tint, its normal tilted by a painting's relief, its
// material, its object's depth and its motion, without the jitter. While the camera looks into an indoor volume (the cutaway),
// whatever stands in front of its box is cut away. The outline pass draws outlined meshes' silhouettes in their outline's color
// at the output's size, without the jitter. Neither draws a cutout card's holes, where its paint covers less than its
// material's cutoff.
// The frame buffer holds FrameData (Frame.hlsl), then the lights, the shadows, each cluster's range of light indices, the
// indices, the indoor volumes and the draws (Draw.hlsl).

#pragma once

#include "Draw.hlsl"
#include "GBuffer.hlsl"

struct SceneConstants
{
    uint Frame;
    uint Draw;
};

[[vk::push_constant]] ConstantBuffer<SceneConstants> Scene : register(b0, space0);

struct VertexOutput
{
    float4 Position : SV_Position;
    float3 WorldPosition : POSITION;
    float3 Normal : NORMAL;
    float2 Uv : TEXCOORD0;
    // Where the vertex stood in the frame before, as the view then saw it.
    float4 PreviousClip : TEXCOORD1;
};

// Where the frame's view, jittered for an upscaler, puts the vertex.
VertexOutput Project(FrameData frame, uint vertexId)
{
    Draw draw = LoadDraw(frame, Scene.Draw);
    ByteAddressBuffer vertices = Buffers[draw.Vertices];
    uint offset = vertexId * VertexStride;
    float3 position = vertices.Load<float3>(offset);
    float3 normal = vertices.Load<float3>(offset + 12);
    float4 world = mul(float4(position, 1), draw.World);

    // The cofactor matrix keeps normals at right angles to surfaces under non-uniform scale; mirroring flips its sign.
    float3x3 basis = (float3x3)draw.World;
    float3x3 cofactor = float3x3(cross(basis[1], basis[2]), cross(basis[2], basis[0]), cross(basis[0], basis[1]));
    float mirror = sign(dot(basis[0], cofactor[0]));

    VertexOutput output;
    output.Position = mul(world, frame.ViewProjection);
    output.WorldPosition = world.xyz;
    output.Normal = mul(normal, cofactor) * mirror;
    output.Uv = vertices.Load<float2>(offset + VertexUv);
    float3 previousPosition = Buffers[draw.PreviousVertices].Load<float3>(offset);
    float4 previousWorld = mul(float4(previousPosition, 1), draw.PreviousWorld);
    output.PreviousClip = mul(previousWorld, frame.PreviousViewProjection);
    return output;
}

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    FrameData frame = LoadFrame(Scene.Frame);
    return Project(frame, vertexId);
}

// For passes drawing at the output's size, such as the outlines' silhouettes: where the view puts the vertex without the jitter.
VertexOutput SilhouetteVertexMain(uint vertexId : SV_VertexID)
{
    FrameData frame = LoadFrame(Scene.Frame);
    VertexOutput output = Project(frame, vertexId);
    output.Position = Unjitter(frame, output.Position);
    return output;
}

// Whether a point stands between the camera and the cutaway's box: outside it, with the box behind it along the view.
bool IsCutAway(FrameData frame, float3 worldPosition)
{
    if (frame.Cut == 0 || IsInBox(worldPosition, frame.CutMin, frame.CutMax))
    {
        return false;
    }

    float3 away = -ToCamera(frame, worldPosition);
    away = select(abs(away) < 1e-6, 1e-6, away);
    float3 toMin = (frame.CutMin - IndoorMargin - worldPosition) / away;
    float3 toMax = (frame.CutMax + IndoorMargin - worldPosition) / away;
    float3 nearSides = min(toMin, toMax);
    float3 farSides = max(toMin, toMax);
    float enter = max(max(nearSides.x, nearSides.y), max(nearSides.z, 0));
    float leave = min(farSides.x, min(farSides.y, farSides.z));
    return enter <= leave;
}

// How much paint covers the draw where the pixel falls: its painting's alpha times its base color's.
float Coverage(VertexOutput input, Draw draw, Material material)
{
    float2 uv = input.Uv;
    uint texture = PaintTexture(draw, material, uv);
    float4 paint = Textures[texture].Sample(Samplers[draw.Sampler], uv);
    return paint.a * draw.BaseColor.a;
}

// The outline pass draws outlined meshes' silhouettes in their outline's color, but for a cutout card's holes.
float4 OutlineMain(VertexOutput input) : SV_Target
{
    FrameData frame = LoadFrame(Scene.Frame);
    Draw draw = LoadDraw(frame, Scene.Draw);
    Material material = LoadMaterial(draw.Material);
    if (Coverage(input, draw, material) < material.Cutoff)
    {
        discard;
    }

    return float4(draw.OutlineColor.rgb, 1);
}

// Tilts the normal by the slope of a painting's relief, so its impasto catches the light without tangents: the relief's
// slope across the uvs, turned into the screen's by the uvs' derivatives, bends the normal by Mikkelsen's surface gradient
// over the position's derivatives. The derivatives come from the caller, outside the branch that reads the relief. Gives how
// high the paint stands there too.
float RaiseRelief(Material material, SamplerState sampler, float2 uv, float2 uvX, float2 uvY, float3 positionX, float3 positionY, inout float3 normal)
{
    Texture2D relief = Textures[material.Relief];
    float width;
    float height;
    relief.GetDimensions(width, height);
    float2 size = float2(width, height);
    float2 texelsX = uvX * size;
    float2 texelsY = uvY * size;
    float level = max(0.5 * log2(max(dot(texelsX, texelsX), dot(texelsY, texelsY))), 0);
    float2 step = exp2(level) / size;
    float left = relief.SampleLevel(sampler, uv - float2(step.x, 0), level).r;
    float right = relief.SampleLevel(sampler, uv + float2(step.x, 0), level).r;
    float up = relief.SampleLevel(sampler, uv - float2(0, step.y), level).r;
    float down = relief.SampleLevel(sampler, uv + float2(0, step.y), level).r;
    float centre = relief.SampleLevel(sampler, uv, level).r;
    float2 slope = float2(right - left, down - up) / (2 * step) * material.ReliefDepth;
    float heightX = dot(slope, uvX);
    float heightY = dot(slope, uvY);
    float3 acrossY = cross(positionY, normal);
    float3 acrossX = cross(normal, positionX);
    float determinant = dot(positionX, acrossY);
    float3 gradient = sign(determinant) * (heightX * acrossY + heightY * acrossX);
    if (material.ReliefDepth > 0)
    {
        normal = normalize(abs(determinant) * normal - gradient);
    }

    return centre;
}

// The relief's height (a share of the painting's impasto range) at which paint stands thick enough to hide the canvas.
static const float ImpastoCover = 0.1;

// A draw's surface at a pixel before any light: its color and coverage, its normal tilted by a painting's relief, and how thick
// its paint stands (1 where the impasto hides the canvas).
struct Surface
{
    float4 Albedo;
    float3 Normal;
    float Impasto;
};

Surface ReadSurface(VertexOutput input, Draw draw, Material material)
{
    SamplerState sampler = Samplers[draw.Sampler];
    float2 uv = input.Uv;
    uint texture = PaintTexture(draw, material, uv);
    Surface surface;
    surface.Albedo = Textures[texture].Sample(sampler, uv) * draw.BaseColor;
    surface.Albedo.rgb *= material.Tint;
    surface.Normal = normalize(input.Normal);
    float2 uvX = ddx(uv);
    float2 uvY = ddy(uv);
    float3 positionX = ddx(input.WorldPosition);
    float3 positionY = ddy(input.WorldPosition);
    surface.Impasto = 0;
    if (material.Texture != None && material.Relief != None)
    {
        float height = RaiseRelief(material, sampler, uv, uvX, uvY, positionX, positionY, surface.Normal);
        surface.Impasto = saturate(height / ImpastoCover);
    }

    return surface;
}

// Whether the cutaway cuts the draw away at a point: anything in front of its box but the meshes it keeps.
bool IsCut(FrameData frame, Draw draw, float3 worldPosition)
{
    return (draw.Flags & FlagUncut) == 0 && IsCutAway(frame, worldPosition);
}
