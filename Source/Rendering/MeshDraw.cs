using System.Numerics;

namespace FlickerVoid.Engine.Rendering;

/// <summary>A mesh part to draw this frame, at its entity's world matrix.</summary>
internal readonly record struct MeshDraw(Matrix4x4 World, GpuMeshPart Part);
