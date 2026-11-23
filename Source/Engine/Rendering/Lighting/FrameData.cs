using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// The start of Shaders/Scene.hlsl's frame buffer: the view, the sun, the shadow atlas, the irradiance volume, and where the
/// lights, shadows and clusters follow, the camera, the fog, the screen shadows, the cutaway and where the indoor volumes follow. 368 bytes.
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
    uint Padding0,
    uint Padding1,
    Vector3 CameraPosition,
    float NearPlane,
    Vector3 FogColor,
    float FogDensity,
    float FogAnisotropy,
    float FogHeight,
    float FogHeightFalloff,
    uint Padding2,
    Matrix4x4 InverseViewProjection,
    uint ScreenShadows,
    float AmbientOcclusion,
    float AmbientOcclusionRadius,
    float ContactShadowLength,
    Vector3 CutMin,
    uint Cut,
    Vector3 CutMax,
    float CutMuted,
    uint IndoorCount,
    uint IndoorsOffset,
    uint Padding3,
    uint Padding4);

