using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Sky;

/// <summary>The sky shader's push constants; mirrors SkyConstants in Sky.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SkyConstants(
    Grading Grade,
    Vector3 CanvasTint,
    uint Canvas,
    uint Frame,
    uint Output,
    uint WrapSampler,
    float Orthographic);
