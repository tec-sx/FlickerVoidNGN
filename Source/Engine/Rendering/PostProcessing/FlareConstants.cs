using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>The push constants of Shaders/Flare.hlsl, for one pass.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct FlareConstants(
    Vector2 SourceTexel,
    Vector2 DestinationSize,
    uint Source,
    uint Destination,
    uint Sampler,
    uint Ghosts,
    uint GhostCount,
    float Threshold,
    float ThresholdRange,
    float Intensity,
    float ChromaShift,
    float EdgeFade);
