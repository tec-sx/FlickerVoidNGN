using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// The start of the frame buffer (Shaders/Frame.hlsl): the view, jittered for an upscaler, the sun, the shadow atlas, the
/// irradiance volume, and where the lights, shadows and clusters follow, the jitter, the camera, the fog, the frame's number for
/// white noise while it is denoised, the screen shadows, the depth fade, the jitter of the frame before, the cutaway and where the indoor volumes follow, the traced shadows and occlusion,
/// the view of the frame before, where the draws follow, the scene's depth and G-buffer, and the scene's acceleration structure
/// while the frame traces rays, then the lit scene behind glass. 516 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct FrameData(
    Matrix4x4 ViewProjection,
    Vector4 DepthAxis,
    Vector3 SunDirection,
    float Ambient,
    Vector2 TargetSize,
    float SliceScale,
    float SliceBias,
    uint LogarithmicSlices,
    uint LightCount,
    uint ClustersOffset,
    uint IndicesOffset,
    uint ShadowsOffset,
    uint SunShadow,
    uint ShadowAtlas,
    uint ClampSampler,
    Vector3 ProbeMin,
    uint Probes,
    Vector3 ProbeSize,
    uint ProbeCountX,
    uint ProbeCountY,
    uint ProbeCountZ,
    Vector2 Jitter,
    Vector3 CameraPosition,
    float NearPlane,
    Vector3 FogColor,
    float FogDensity,
    float FogAnisotropy,
    float FogHeight,
    float FogHeightFalloff,
    uint NoiseFrame,
    Matrix4x4 InverseViewProjection,
    uint ScreenShadows,
    float AmbientOcclusion,
    float AmbientOcclusionRadius,
    float ContactShadowLength,
    float FocusDepth,
    float NearStart,
    float NearEnd,
    float NearAmount,
    Vector3 FarColor,
    float FarStart,
    float FarEnd,
    float FarAmount,
    Vector2 PreviousJitter,
    Vector3 CutMin,
    uint Cut,
    Vector3 CutMax,
    float CutMuted,
    uint IndoorCount,
    uint IndoorsOffset,
    uint RayShadows,
    uint RayOcclusion,
    Matrix4x4 PreviousViewProjection,
    uint DrawsOffset,
    uint Depth,
    uint GBufferAlbedo,
    uint GBufferNormal,
    uint GBufferMaterial,
    uint GBufferObjectDepth,
    uint GBufferMotion,
    uint AccelerationStructure,
    uint Behind);

