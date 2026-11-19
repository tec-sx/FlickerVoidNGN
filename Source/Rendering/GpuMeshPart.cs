using System.Numerics;

using FlickerVoid.Engine.Graphics;

namespace FlickerVoid.Engine.Rendering;

/// <summary>A mesh part uploaded to the GPU; the texture belongs to the part unless it is the shared white one.</summary>
internal sealed record GpuMeshPart(IBuffer Vertices, IBuffer Indices, int IndexCount, ITexture? Texture, Vector4 BaseColor) : IDisposable
{
    public void Dispose()
    {
        Texture?.Dispose();
        Indices.Dispose();
        Vertices.Dispose();
    }
}
