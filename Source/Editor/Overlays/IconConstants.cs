using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Editor.Overlays;

/// <summary>The icon shader's push constants; mirrors IconConstants in Icon.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct IconConstants(Matrix4x4 ViewProjection, uint Icons, uint Sampler, Vector2 TargetSize);
