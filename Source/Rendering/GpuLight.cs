using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// A point or spot light as Shaders/Scene.hlsl reads it: 48 bytes. The color is scaled by the intensity; a point light's cone
/// cosines lie below -1, so its whole sphere is inside the cone.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GpuLight(Vector3 Position, float Range, Vector3 Color, float CosOuter, Vector3 Direction, float CosInner);
