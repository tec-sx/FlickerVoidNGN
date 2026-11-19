using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>A mesh part to draw this frame, at its entity's world matrix, from its own vertices or the ones skinning posed.</summary>
internal readonly record struct MeshDraw(Matrix4x4 World, GpuMeshPart Part, IBuffer Vertices);

