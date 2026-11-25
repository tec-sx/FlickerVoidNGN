using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>The push constants of Shaders/DebugView.hlsl: the frame's buffer and the <see cref="DebugView"/> to draw.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DebugViewConstants(uint Frame, uint View);
