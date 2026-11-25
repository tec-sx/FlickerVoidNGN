using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>The push constants of Shaders/Post.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PostConstants(
    Grading Grade,
    float Bloom,
    float CanvasIntensity,
    uint Scene,
    uint BloomTexture,
    uint Canvas,
    uint ClampSampler,
    uint WrapSampler,
    float CanvasImageHeight,
    Vector3 CanvasTint,
    float CanvasPanRight,
    float CanvasPanUp);
