using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Instancing;

/// <summary>
/// The push constants of Shaders/Instancing.hlsl: the frame's buffer, the batch's copies, the visible list and the arguments the
/// cull writes, how many copies and parts there are, the frame's tally, the copies' scale range as two halves, and the sphere round
/// the model.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CullConstants(
    uint Frame,
    uint Copies,
    uint Visible,
    uint Arguments,
    uint Count,
    uint Parts,
    uint Counted,
    uint Scales,
    Vector3 Center,
    float Radius);
