// Draws a mesh part whose vertices (float3 position, float3 normal, float2 uv) are pulled from a bindless buffer, as its record
// among the frame buffer's draws says (pushed by its number). The geometry pass leaves its surface in the G-buffer (GBuffer.cs)
// for Lighting.hlsl: its base color times its texture and its material's tint, its normal tilted by a painting's relief, its
// material, its object's depth and its motion. While the camera looks into an indoor volume (the cutaway), whatever stands in
// front of its box is cut away. The outline pass draws outlined meshes' silhouettes in their outline's color.
// The frame buffer holds FrameData (Frame.hlsl), then the lights, the shadows, each cluster's range of light indices, the
// indices, the indoor volumes and the draws.

#include "Frame.hlsl"
#include "GBuffer.hlsl"
#include "Material.hlsl"

struct SceneConstants
{
    uint Frame;
    uint Draw;
};

// A draw's record (GpuDraw.cs): where its mesh stands this frame and the frame before, its vertices then and now, its looks,
// its outline's color, its MeshFlags, and the centre of its box with how wholly it takes that depth for the depth tone.
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
    float3 Center;
    float Wholeness;
};

static const uint DrawSize = 208;

[[vk::push_constant]] ConstantBuffer<SceneConstants> Scene : register(b0, space0);

static const uint VertexStride = 32;

struct VertexOutput
{
    float4 Position : SV_Position;
    float3 WorldPosition : POSITION;
    float3 Normal : NORMAL;
    float2 Uv : TEXCOORD0;
    // Where the vertex stood in the frame before, as the view then saw it.
    float4 PreviousClip : TEXCOORD1;
};

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
    draw.Center = buffer.Load<float3>(offset + 192);
    draw.Wholeness = buffer.Load<float>(offset + 204);
    return draw;
}

VertexOutput VertexMain(uint vertexId : SV_VertexID)
{
    FrameData frame = LoadFrame(Scene.Frame);
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
    output.Uv = vertices.Load<float2>(offset + 24);
    float3 previousPosition = Buffers[draw.PreviousVertices].Load<float3>(offset);
    float4 previousWorld = mul(float4(previousPosition, 1), draw.PreviousWorld);
    output.PreviousClip = mul(previousWorld, frame.PreviousViewProjection);
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

// The outline pass draws outlined meshes' silhouettes in their outline's color.
float4 OutlineMain(VertexOutput input) : SV_Target
{
    FrameData frame = LoadFrame(Scene.Frame);
    Draw draw = LoadDraw(frame, Scene.Draw);
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
    uint texture = draw.Texture;
    float2 uv = input.Uv;
    if (material.Texture != None)
    {
        texture = material.Texture;
        uv *= material.TextureScale;
    }

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

// The G-buffer's targets, in GBuffer.Formats' order.
struct GeometryOutput
{
    float4 Albedo : SV_Target0;
    float2 Normal : SV_Target1;
    float4 Material : SV_Target2;
    float ObjectDepth : SV_Target3;
    float2 Motion : SV_Target4;
};

// The geometry pass leaves the surface for lighting, unlit, but where the cutaway cuts it away. The object depth leans from the
// pixel's own view depth to its box centre's as the draw is small (Draw.Wholeness), and the motion is where the surface stood in
// the frame before less where it stands now, in pixels.
GeometryOutput GeometryMain(VertexOutput input)
{
    FrameData frame = LoadFrame(Scene.Frame);
    Draw draw = LoadDraw(frame, Scene.Draw);
    if (IsCut(frame, draw, input.WorldPosition))
    {
        discard;
    }

    Material material = LoadMaterial(draw.Material);
    Surface surface = ReadSurface(input, draw, material);
    GeometryOutput output;
    output.Albedo = surface.Albedo;
    output.Normal = EncodeNormal(surface.Normal);
    output.Material = EncodeMaterial(draw.Material, surface.Impasto, draw.Flags);
    float depth = dot(float4(input.WorldPosition, 1), frame.DepthAxis);
    float centerDepth = dot(float4(draw.Center, 1), frame.DepthAxis);
    output.ObjectDepth = lerp(depth, centerDepth, draw.Wholeness);
    float2 previous = ClipToPixel(frame, input.PreviousClip);
    output.Motion = previous - input.Position.xy;
    return output;
}
