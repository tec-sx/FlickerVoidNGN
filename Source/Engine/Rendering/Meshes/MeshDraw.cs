using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// A part of a render mesh (by the mesh's id) to draw this frame, at its entity's world matrix, from its own vertices or the ones
/// skinning posed, with where it stood and how it was posed in the frame before, its material's parameters and its entity's
/// outline, if it has one, and its <see cref="MeshFlags"/>; the box around it in the world, none for a posed part, which is never
/// culled, and whether the view draws it: neither shadow nor outline only, and not culled.
/// </summary>
internal readonly record struct MeshDraw(
    int Mesh,
    Matrix4x4 World,
    Matrix4x4 PreviousWorld,
    GpuMeshPart Part,
    IBuffer Vertices,
    IBuffer PreviousVertices,
    GpuMaterial Material,
    Outline? Outline,
    MeshFlags Flags,
    Bounds? Bounds,
    bool InView);
