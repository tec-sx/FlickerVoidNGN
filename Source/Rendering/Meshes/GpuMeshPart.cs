using System.Numerics;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// A mesh part uploaded to the GPU, with its joints and weights when it is skinned; the texture belongs to the part unless
/// it is the shared white one.
/// </summary>
internal sealed record GpuMeshPart(IBuffer Vertices, int VertexCount, IBuffer Indices, int IndexCount, IBuffer? Skin, ITexture? Texture, Vector4 BaseColor) : IDisposable
{
    public void Dispose()
    {
        Texture?.Dispose();
        Skin?.Dispose();
        Indices.Dispose();
        Vertices.Dispose();
    }
}

