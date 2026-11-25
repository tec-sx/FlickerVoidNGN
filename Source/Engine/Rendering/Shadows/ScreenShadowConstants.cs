using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>The push constants of Shaders/ScreenShadows.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ScreenShadowConstants(uint Frame, uint Output);

