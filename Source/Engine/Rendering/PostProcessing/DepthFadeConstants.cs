using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>The push constants of Shaders/DepthFade.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DepthFadeConstants(Grading Grade, uint Frame, uint Output);
