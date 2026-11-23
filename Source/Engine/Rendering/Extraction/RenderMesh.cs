using System.Numerics;

using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Outlines;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>
/// A mesh to draw with its parts' materials; <paramref name="Id"/> stays the same across frames so posed buffers can be
/// reused. A <paramref name="ShadowOnly"/> mesh only casts shadows.
/// </summary>
public readonly record struct RenderMesh(int Id, Matrix4x4 World, string Mesh, MeshAnimator? Animator, string[]? Materials = null, Outline? Outline = null, MeshFlags Flags = MeshFlags.None);
