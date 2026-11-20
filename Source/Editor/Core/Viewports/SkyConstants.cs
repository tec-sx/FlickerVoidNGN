using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Editor.Core.Viewports;

/// <summary>The sky shader's push constants; mirrors SkyConstants in Sky.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SkyConstants(Matrix4x4 InverseViewProjection, float Orthographic, Vector3 Padding = default);
