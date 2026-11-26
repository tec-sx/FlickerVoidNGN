using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>The push constants of Shaders/Lighting.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct LightingConstants(uint Frame, uint Output);
