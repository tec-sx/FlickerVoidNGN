using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Graphics.Tests;

/// <summary>The push constants of <see cref="TestShaders"/>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DrawConstants(uint VertexBuffer, uint Texture, uint Sampler, uint Unused, Vector4 Color);
