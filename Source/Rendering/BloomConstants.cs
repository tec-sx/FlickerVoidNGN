using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering;

/// <summary>The push constants of Shaders/Bloom.hlsl, for one level.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct BloomConstants(Vector2 SourceTexel, Vector2 DestinationSize, uint Source, uint Destination, uint Sampler);
