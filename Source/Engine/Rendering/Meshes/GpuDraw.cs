using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// A draw's record in the frame buffer, which Shaders/Draw.hlsl reads by the draw's number: where its mesh stands this frame
/// and the frame before, its vertices then and now, its looks, its outline's color (none when transparent), its
/// <see cref="Extraction.MeshFlags"/>, its indices, for rays to find a hit triangle's corners, and the centre of its box with how
/// wholly it takes that depth for the depth tone (<see cref="ObjectDepth"/>). 208 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GpuDraw(
    Matrix4x4 World,
    Matrix4x4 PreviousWorld,
    Vector4 BaseColor,
    Vector4 OutlineColor,
    uint Vertices,
    uint PreviousVertices,
    uint Texture,
    uint Sampler,
    uint Material,
    uint Flags,
    uint Indices,
    uint Padding,
    Vector3 Center,
    float Wholeness);
