using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>The push constants of Shaders/DebugView.hlsl: the frame's buffer, the <see cref="DebugView"/> to draw and render pixels to an output pixel.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DebugViewConstants(uint Frame, uint View, Vector2 Scale);
