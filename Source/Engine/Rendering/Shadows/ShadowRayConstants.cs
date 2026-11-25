using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>The push constants of Shaders/ShadowRays.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ShadowRayConstants(
    Vector4 PreviousDepthAxis,
    uint Frame,
    uint Raw,
    uint RawOcclusion,
    uint Visibility,
    uint History,
    uint PreviousVisibility,
    uint PreviousHistory,
    uint Number,
    float SunRadius,
    uint Lamp0,
    uint Lamp1,
    uint Lamp2);
