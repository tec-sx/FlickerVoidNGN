using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering;

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
    uint Paper,
    uint ClampSampler,
    uint WrapSampler);
