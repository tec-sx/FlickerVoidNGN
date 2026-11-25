using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Upscaling;

/// <summary>The push constants of Shaders/Upscale.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct UpscaleMaskConstants(uint Scene, uint Output, uint ClampSampler, uint Padding, Vector2 Jitter, Vector2 RenderSize);
