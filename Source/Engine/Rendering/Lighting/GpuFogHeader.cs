using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// The start of the fog's own data (Lighting.hlsl's LoadFogData): how many fog volumes follow it, the mood's noise share and wisp
/// size, and how far the wind has carried the noise so far.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GpuFogHeader(uint VolumeCount, float Noise, float NoiseScale, float Padding, Vector3 Drift, float Padding2 = 0);
