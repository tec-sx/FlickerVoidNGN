using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>
/// The push constants of Shaders/Shadow.hlsl, for one mesh part in one shadow view: where it lands, its vertices, and the frame
/// buffer and draw number a cutout part reads its paint through.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ShadowConstants(Matrix4x4 WorldViewProjection, uint Vertices, uint Frame, uint Draw);
