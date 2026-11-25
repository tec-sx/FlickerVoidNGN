using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>
/// A shadow as Shaders/Lighting.hlsl reads it: 96 bytes. The matrix maps world space to atlas coordinates and depth; the rect
/// keeps filtering inside the tile (min u, min v, max u, max v); a texel covers TexelPerDistance * distance +
/// TexelConstant world units, for the normal offset that keeps surfaces from shadowing themselves.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 96)]
internal readonly record struct GpuShadow(Matrix4x4 WorldToAtlas, Vector4 Rect, float TexelPerDistance, float TexelConstant);

