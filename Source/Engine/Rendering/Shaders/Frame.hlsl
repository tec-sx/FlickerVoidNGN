// The bindless tables and the frame buffer's FrameData, shared by the scene renderer's shaders, which include this file.
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
    float3 CameraPosition;
    float NearPlane;
    float3 FogColor;
    float FogDensity;
    float FogAnisotropy;
    float FogHeight;
    float FogHeightFalloff;
    float4x4 InverseViewProjection;
    uint ScreenShadows;
    float AmbientOcclusion;
    float AmbientOcclusionRadius;
    float ContactShadowLength;
    float FocusDepth;
    float NearStart;
    float NearEnd;
    float NearAmount;
    float3 FarColor;
    float FarStart;
    float FarEnd;
    float FarAmount;
    float3 CutMin;
    uint Cut;
    float3 CutMax;
    float CutMuted;
    uint IndoorCount;
    uint IndoorsOffset;
    float4x4 PreviousViewProjection;
    uint DrawsOffset;
    // The frame buffer's own bindless index, which LoadFrame was given.
    uint Buffer;
};

static const uint FrameDataSize = 496;
static const uint None = 0xFFFFFFFF;

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
    frame.CameraPosition = buffer.Load<float3>(192);
    frame.NearPlane = buffer.Load<float>(204);
    frame.FogColor = buffer.Load<float3>(208);
    frame.FogDensity = buffer.Load<float>(220);
    frame.FogAnisotropy = buffer.Load<float>(224);
    frame.FogHeight = buffer.Load<float>(228);
    frame.FogHeightFalloff = buffer.Load<float>(232);
    frame.InverseViewProjection = float4x4(buffer.Load<float4>(240), buffer.Load<float4>(256), buffer.Load<float4>(272), buffer.Load<float4>(288));
    frame.ScreenShadows = buffer.Load<uint>(304);
    frame.AmbientOcclusion = buffer.Load<float>(308);
    frame.AmbientOcclusionRadius = buffer.Load<float>(312);
    frame.ContactShadowLength = buffer.Load<float>(316);
    frame.FocusDepth = buffer.Load<float>(320);
    frame.NearStart = buffer.Load<float>(324);
    frame.NearEnd = buffer.Load<float>(328);
    frame.NearAmount = buffer.Load<float>(332);
    frame.FarColor = buffer.Load<float3>(336);
    frame.FarStart = buffer.Load<float>(348);
    frame.FarEnd = buffer.Load<float>(352);
    frame.FarAmount = buffer.Load<float>(356);
    frame.CutMin = buffer.Load<float3>(368);
    frame.Cut = buffer.Load<uint>(380);
    frame.CutMax = buffer.Load<float3>(384);
    frame.CutMuted = buffer.Load<float>(396);
    frame.IndoorCount = buffer.Load<uint>(400);
    frame.IndoorsOffset = buffer.Load<uint>(404);
    frame.PreviousViewProjection = float4x4(buffer.Load<float4>(416), buffer.Load<float4>(432), buffer.Load<float4>(448), buffer.Load<float4>(464));
    frame.DrawsOffset = buffer.Load<uint>(480);
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

// The pixel (with fraction) a world position lands on, and its depth in front of the camera.
float3 ProjectToPixel(FrameData frame, float3 position)
{
    float4 clip = mul(float4(position, 1), frame.ViewProjection);
    float2 ndc = clip.xy / clip.w;
    float2 pixel = float2(ndc.x * 0.5 + 0.5, 0.5 - ndc.y * 0.5) * frame.TargetSize;
    float depth = dot(float4(position, 1), frame.DepthAxis);
    return float3(pixel, depth);
}
