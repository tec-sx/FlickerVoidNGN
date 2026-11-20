using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>The push constants of Shaders/Shadow.hlsl, for one mesh part in one shadow view.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ShadowConstants(Matrix4x4 WorldViewProjection, uint Vertices);

