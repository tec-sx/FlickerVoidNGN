using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>The push constants of Shaders/Post.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PostConstants(
    Vector3 Tint,
    float Exposure,
    float Contrast,
    float Saturation,
    float Bloom,
    float Grain,
    uint Scene,
    uint BloomTexture,
    uint Canvas,
    uint ClampSampler,
    uint WrapSampler,
    uint Tonemapper,
    float WhitePoint,
    float CanvasGrain,
    CanvasView CanvasView,
    Vector3 CanvasTint,
    float CanvasTiling);

