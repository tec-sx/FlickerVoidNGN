using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>An indoor volume's box and ambient share as Shaders/Lighting.hlsl reads it from the frame buffer: 32 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GpuIndoor(Vector3 Min, float Ambient, Vector3 Max, float Padding = 0);
