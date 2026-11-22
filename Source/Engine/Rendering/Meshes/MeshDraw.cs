using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Rendering.Outlines;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// A mesh part to draw this frame, at its entity's world matrix, from its own vertices or the ones skinning posed, with its
/// material's parameters and its entity's outline, if it has one.
/// </summary>
internal readonly record struct MeshDraw(Matrix4x4 World, GpuMeshPart Part, IBuffer Vertices, IBuffer Material, Outline? Outline = null);

