using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>A vertex as the scene shader reads it from a bindless buffer: 32 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct MeshVertex(Vector3 Position, Vector3 Normal, Vector2 Uv);

