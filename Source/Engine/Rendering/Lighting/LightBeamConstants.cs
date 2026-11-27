using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>The push constants of Shaders/LightBeams.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct LightBeamConstants(
    Vector3 Center,
    float Spacing,
    Vector3 Color,
    float HalfWidth,
    int CellX,
    int CellZ,
    float Blend,
    float Lift,
    uint Frame,
    uint Beams,
    uint Output,
    uint Padding);
