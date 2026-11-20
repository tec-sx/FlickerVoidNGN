using System.Numerics;

using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>
/// A mesh to draw with its parts' materials; <paramref name="Id"/> stays the same across frames so posed buffers can be
/// reused.
/// </summary>
public readonly record struct RenderMesh(int Id, Matrix4x4 World, string Mesh, MeshAnimator? Animator, string[]? Materials = null);
