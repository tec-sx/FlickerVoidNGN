// The bindless tables, the frame buffer's FrameData and the view and indoor volume helpers the scene renderer's shaders share,
// which include this file.
// FrameData mirrors FrameData.cs.

#pragma once

Texture2D Textures[] : register(t0, space1);
ByteAddressBuffer Buffers[] : register(t0, space2);
SamplerState Samplers[] : register(s0, space3);
RWTexture2D<float4> RWTextures[] : register(u0, space4);

struct FrameData
{
    float4x4 ViewProjection;
    float4 DepthAxis;
    float3 SunDirection;
    float Ambient;
    float2 TargetSize;
    float SliceScale;
    float SliceBias;
    uint LogarithmicSlices;
    uint LightCount;
    uint ClustersOffset;
    uint IndicesOffset;
    uint ShadowsOffset;
    uint SunShadow;
    uint ShadowAtlas;
    uint ClampSampler;
    float3 ProbeMin;
    uint Probes;
    float3 ProbeSize;
    uint3 ProbeCounts;
    // How far this frame's view, and so ViewProjection, was moved for an upscaler, in render pixels right and down; zero without
    // one. TargetSize is the render size.
    float2 Jitter;
    float3 CameraPosition;
    float NearPlane;
    float3 FogColor;
    float FogDensity;
    float FogAnisotropy;
    float FogHeight;
    float FogHeightFalloff;
    // The frame's number while an upscaler denoises it, so its noise is drawn afresh each frame as white noise, as Ray
    // Reconstruction asks; else None, and noise may hold a pattern.
    uint NoiseFrame;
    float4x4 InverseViewProjection;
    uint ScreenShadows;
    float AmbientOcclusion;
    float AmbientOcclusionRadius;
    float ContactShadowLength;
    float FocusDepth;
    float NearStart;
    float NearEnd;
    float NearAmount;
    float NearBurn;
    float FarStart;
    float FarEnd;
    float FarAmount;
    // The frame before's jitter; PreviousViewProjection is without it.
    float2 PreviousJitter;
    float3 CutMin;
    uint Cut;
    float3 CutMax;
    float CutMuted;
    uint IndoorCount;
    uint IndoorsOffset;
    // The traced shadows while the frame traces rays (ShadowRays.hlsl), the sun's in red and the lamps' after it, and the
    // traced ambient occlusion in red, else None.
    uint RayShadows;
    uint RayOcclusion;
    float4x4 PreviousViewProjection;
    uint DrawsOffset;
    // The scene's depth and the G-buffer's targets (GBuffer.cs), by their bindless indices.
    uint Depth;
    uint GBufferAlbedo;
    uint GBufferNormal;
    uint GBufferMaterial;
    uint GBufferObjectDepth;
    uint GBufferMotion;
    // The scene's top-level acceleration structure while the frame traces rays, else 0, an empty one; the table of them
    // (t0, space5) exists only on devices with ray queries.
    uint AccelerationStructure;
    float3 SunLight;
    // The frame buffer's own bindless index, which LoadFrame was given.
    uint Buffer;
    // The haze (Haze.hlsl): its colour as it looks on screen, its density, its start behind the focus, its most, and its height
    // and the falloff above it.
    float3 HazeColor;
    float HazeDensity;
    float HazeStart;
    float HazeMax;
    float HazeHeight;
    float HazeHeightFalloff;
    // The depth axis of the frame before, which reprojects the fog volume's history.
    float4 PreviousDepthAxis;
    // How far the fog is lit along the view, and the integrated fog volume's bindless index (Lighting.hlsl), None without fog.
    float FogRange;
    uint FogVolume;
    // How hard it rains, from 0 to 1 (LightingMood.Rain), which wets outdoor surfaces.
    float Rain;
    // The lit scene before glass was drawn over it (TransparentPass), which glass bends and blurs.
    uint Behind;
    // The world glossy surfaces reflect (LightingMood.Reflection), an equirectangular image with its mips, else None; the sampler
    // it is read with and how bright it is.
    uint Reflection;
    uint ReflectionSampler;
    float ReflectionIntensity;
    // The colour light in the sun's shadow is multiplied by, and how much of the sun's light the shadow takes away.
    float3 ShadowColor;
    float ShadowDarkness;
};

static const uint FrameDataSize = 624;
static const uint None = 0xFFFFFFFF;

// PCG's output hash (Jarzynski and Olano 2020, "Hash Functions for GPU Rendering").
uint Hash(uint value)
{
    uint state = value * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    return (word >> 22u) ^ word;
}

// A number from 0 to 1 of a hash's top 24 bits, which a float holds exactly.
float HashToUnit(uint hash)
{
    return float(hash >> 8) / 16777216.0;
}

FrameData LoadFrame(uint index)
{
    ByteAddressBuffer buffer = Buffers[index];
    FrameData frame;
    // Rows of a System.Numerics matrix, which multiplies row vectors.
    frame.ViewProjection = float4x4(buffer.Load<float4>(0), buffer.Load<float4>(16), buffer.Load<float4>(32), buffer.Load<float4>(48));
    frame.DepthAxis = buffer.Load<float4>(64);
    frame.SunDirection = buffer.Load<float3>(80);
    frame.Ambient = buffer.Load<float>(92);
    frame.TargetSize = buffer.Load<float2>(96);
    frame.SliceScale = buffer.Load<float>(104);
    frame.SliceBias = buffer.Load<float>(108);
    frame.LogarithmicSlices = buffer.Load<uint>(112);
    frame.LightCount = buffer.Load<uint>(116);
    frame.ClustersOffset = buffer.Load<uint>(120);
    frame.IndicesOffset = buffer.Load<uint>(124);
    frame.ShadowsOffset = buffer.Load<uint>(128);
    frame.SunShadow = buffer.Load<uint>(132);
    frame.ShadowAtlas = buffer.Load<uint>(136);
    frame.ClampSampler = buffer.Load<uint>(140);
    frame.ProbeMin = buffer.Load<float3>(144);
    frame.Probes = buffer.Load<uint>(156);
    frame.ProbeSize = buffer.Load<float3>(160);
    frame.ProbeCounts = buffer.Load<uint3>(172);
    frame.Jitter = buffer.Load<float2>(184);
    frame.CameraPosition = buffer.Load<float3>(192);
    frame.NearPlane = buffer.Load<float>(204);
    frame.FogColor = buffer.Load<float3>(208);
    frame.FogDensity = buffer.Load<float>(220);
    frame.FogAnisotropy = buffer.Load<float>(224);
    frame.FogHeight = buffer.Load<float>(228);
    frame.FogHeightFalloff = buffer.Load<float>(232);
    frame.NoiseFrame = buffer.Load<uint>(236);
    frame.InverseViewProjection = float4x4(buffer.Load<float4>(240), buffer.Load<float4>(256), buffer.Load<float4>(272), buffer.Load<float4>(288));
    frame.ScreenShadows = buffer.Load<uint>(304);
    frame.AmbientOcclusion = buffer.Load<float>(308);
    frame.AmbientOcclusionRadius = buffer.Load<float>(312);
    frame.ContactShadowLength = buffer.Load<float>(316);
    frame.FocusDepth = buffer.Load<float>(320);
    frame.NearStart = buffer.Load<float>(324);
    frame.NearEnd = buffer.Load<float>(328);
    frame.NearAmount = buffer.Load<float>(332);
    frame.NearBurn = buffer.Load<float>(336);
    frame.FarStart = buffer.Load<float>(348);
    frame.FarEnd = buffer.Load<float>(352);
    frame.FarAmount = buffer.Load<float>(356);
    frame.PreviousJitter = buffer.Load<float2>(360);
    frame.CutMin = buffer.Load<float3>(368);
    frame.Cut = buffer.Load<uint>(380);
    frame.CutMax = buffer.Load<float3>(384);
    frame.CutMuted = buffer.Load<float>(396);
    frame.IndoorCount = buffer.Load<uint>(400);
    frame.IndoorsOffset = buffer.Load<uint>(404);
    frame.RayShadows = buffer.Load<uint>(408);
    frame.RayOcclusion = buffer.Load<uint>(412);
    frame.PreviousViewProjection = float4x4(buffer.Load<float4>(416), buffer.Load<float4>(432), buffer.Load<float4>(448), buffer.Load<float4>(464));
    frame.DrawsOffset = buffer.Load<uint>(480);
    frame.Depth = buffer.Load<uint>(484);
    frame.GBufferAlbedo = buffer.Load<uint>(488);
    frame.GBufferNormal = buffer.Load<uint>(492);
    frame.GBufferMaterial = buffer.Load<uint>(496);
    frame.GBufferObjectDepth = buffer.Load<uint>(500);
    frame.GBufferMotion = buffer.Load<uint>(504);
    frame.AccelerationStructure = buffer.Load<uint>(508);
    frame.SunLight = buffer.Load<float3>(512);
    frame.HazeColor = buffer.Load<float3>(528);
    frame.HazeDensity = buffer.Load<float>(540);
    frame.HazeStart = buffer.Load<float>(544);
    frame.HazeMax = buffer.Load<float>(548);
    frame.HazeHeight = buffer.Load<float>(552);
    frame.HazeHeightFalloff = buffer.Load<float>(556);
    frame.PreviousDepthAxis = buffer.Load<float4>(560);
    frame.FogRange = buffer.Load<float>(576);
    frame.FogVolume = buffer.Load<uint>(580);
    frame.Rain = buffer.Load<float>(584);
    frame.Behind = buffer.Load<uint>(592);
    frame.Reflection = buffer.Load<uint>(596);
    frame.ReflectionSampler = buffer.Load<uint>(600);
    frame.ReflectionIntensity = buffer.Load<float>(604);
    frame.ShadowColor = buffer.Load<float3>(608);
    frame.ShadowDarkness = buffer.Load<float>(620);
    frame.Buffer = index;
    return frame;
}

// The world position seen at a pixel centre with the given depth buffer value.
float3 WorldAtPixel(FrameData frame, float2 pixel, float depth)
{
    float2 uv = pixel / frame.TargetSize;
    float4 ndc = float4(uv.x * 2 - 1, 1 - uv.y * 2, depth, 1);
    float4 world = mul(ndc, frame.InverseViewProjection);
    return world.xyz / world.w;
}

// The clip position without the frame's jitter, as passes drawing at the output's size want.
float4 Unjitter(FrameData frame, float4 clip)
{
    float2 shift = float2(2, -2) * frame.Jitter / frame.TargetSize;
    clip.xy -= shift * clip.w;
    return clip;
}

// The pixel (with fraction) a clip position lands on.
float2 ClipToPixel(FrameData frame, float4 clip)
{
    float2 ndc = clip.xy / clip.w;
    return float2(ndc.x * 0.5 + 0.5, 0.5 - ndc.y * 0.5) * frame.TargetSize;
}

// The pixel (with fraction) a world position lands on, and its depth in front of the camera.
float3 ProjectToPixel(FrameData frame, float3 position)
{
    float4 clip = mul(float4(position, 1), frame.ViewProjection);
    float2 pixel = ClipToPixel(frame, clip);
    float depth = dot(float4(position, 1), frame.DepthAxis);
    return float3(pixel, depth);
}

// The direction from the point to the camera: toward its eye in perspective, against its view in orthographic.
float3 ToCamera(FrameData frame, float3 worldPosition)
{
    if (frame.LogarithmicSlices != 0)
    {
        return normalize(frame.CameraPosition - worldPosition);
    }

    return -normalize(frame.DepthAxis.xyz);
}

// Indoor volumes reach a little past their boxes, so walls, floors and ceilings standing on them count as inside.
static const float IndoorMargin = 0.05;

bool IsInBox(float3 position, float3 boxMin, float3 boxMax)
{
    return all(position >= boxMin - IndoorMargin) && all(position <= boxMax + IndoorMargin);
}

// Greys and darkens the world outside the cutaway's box by its muting, so the room it looks into stands out.
float3 MuteOutside(FrameData frame, float3 color, float3 worldPosition)
{
    if (frame.Cut == 0 || frame.CutMuted <= 0 || IsInBox(worldPosition, frame.CutMin, frame.CutMax))
    {
        return color;
    }

    float grey = dot(color, float3(0.2126, 0.7152, 0.0722));
    float3 muted = lerp(color, grey, frame.CutMuted);
    return muted * (1 - (0.5 * frame.CutMuted));
}

static const uint IndoorSize = 32;

// The share of the mood's ambient light at a point: the least of the indoor volumes it stands in, else all of it.
// Whether the point stands inside any indoor volume.
bool IsIndoors(FrameData frame, float3 worldPosition)
{
    ByteAddressBuffer buffer = Buffers[frame.Buffer];
    for (uint index = 0; index < frame.IndoorCount; index++)
    {
        uint offset = frame.IndoorsOffset + index * IndoorSize;
        float3 boxMin = buffer.Load<float3>(offset);
        float3 boxMax = buffer.Load<float3>(offset + 16);
        if (IsInBox(worldPosition, boxMin, boxMax))
        {
            return true;
        }
    }

    return false;
}

float IndoorAmbient(FrameData frame, float3 worldPosition)
{
    ByteAddressBuffer buffer = Buffers[frame.Buffer];
    float ambient = 1;
    for (uint index = 0; index < frame.IndoorCount; index++)
    {
        uint offset = frame.IndoorsOffset + index * IndoorSize;
        float3 boxMin = buffer.Load<float3>(offset);
        float boxAmbient = buffer.Load<float>(offset + 12);
        float3 boxMax = buffer.Load<float3>(offset + 16);
        if (IsInBox(worldPosition, boxMin, boxMax))
        {
            ambient = min(ambient, boxAmbient);
        }
    }

    return ambient;
}
