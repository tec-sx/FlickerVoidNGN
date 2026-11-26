using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>The push constants of Shaders/Glare.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GlareConstants(
    uint Source,
    uint Points,
    uint Width,
    uint Height,
    float Threshold,
    float Intensity,
    float Length,
    float StreakWidth,
    float Angle,
    float Aspect);
