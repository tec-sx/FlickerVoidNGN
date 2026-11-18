using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Samples.TexturedMesh;

/// <summary>
/// The push constants of Content/Mesh.hlsl: a row-major matrix, the light's direction in the mesh's space, then the
/// bindless indices of the vertices, texture and sampler.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct MeshConstants(Matrix4x4 WorldViewProjection, Vector3 LightDirection, uint Vertices, uint Texture, uint Sampler);
