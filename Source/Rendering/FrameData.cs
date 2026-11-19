using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// The start of Shaders/Scene.hlsl's frame buffer: the view, the sun, the shadow atlas, the irradiance volume, and where the
/// lights, shadows and clusters follow. 192 bytes.
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
    uint Padding1);
