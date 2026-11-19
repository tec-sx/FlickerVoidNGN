using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering;

/// <summary>The four joints that move a skinned vertex and how much each one counts, as the skinning shader reads them: 32 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct SkinVertex(uint Joint0, uint Joint1, uint Joint2, uint Joint3, Vector4 Weights);
