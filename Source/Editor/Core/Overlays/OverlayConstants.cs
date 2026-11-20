using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Editor.Core.Overlays;

/// <summary>The overlay shader's push constants; mirrors OverlayConstants in Overlay.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct OverlayConstants(Matrix4x4 ViewProjection, uint Vertices, uint Padding0 = 0, uint Padding1 = 0, uint Padding2 = 0);
