using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering;

/// <summary>Imports each glTF model the first time it is drawn and keeps it on the GPU, its textures with full mip chains.</summary>
internal sealed class MeshLibrary : IDisposable
{
    private readonly IGraphicsDevice _device;
    private readonly IContentManager _content;
    private readonly Dictionary<string, IReadOnlyList<GpuMeshPart>> _meshes = [];

    public MeshLibrary(IGraphicsDevice device, IContentManager content)
    {
        _device = device;
        _content = content;
    }

    public IReadOnlyList<GpuMeshPart> Get(string path)
    {
        string fullPath = _content.GetFullPath(path);
        if (_meshes.TryGetValue(fullPath, out IReadOnlyList<GpuMeshPart>? loaded))
        {
            return loaded;
        }

        IReadOnlyList<MeshPart> parts = GltfImporter.Load(fullPath);
        GpuMeshPart[] uploaded = [.. parts.Select(part => Upload(part, path))];
        _meshes.Add(fullPath, uploaded);
        return uploaded;
    }

    public void Dispose()
    {
        foreach (IReadOnlyList<GpuMeshPart> parts in _meshes.Values)
        {
            foreach (GpuMeshPart part in parts)
            {
                part.Dispose();
            }
        }

        _meshes.Clear();
    }

    private GpuMeshPart Upload(MeshPart part, string name)
    {
        byte[] vertexData = MemoryMarshal.AsBytes(part.Vertices.AsSpan()).ToArray();
        byte[] indexData = MemoryMarshal.AsBytes(part.Indices.AsSpan()).ToArray();
        BufferDescription vertexDescription = new(vertexData.Length, BufferUsage.ShaderRead) { Name = $"{name} vertices" };
        BufferDescription indexDescription = new(indexData.Length, BufferUsage.Index) { Name = $"{name} indices" };
        IBuffer vertices = _device.CreateBuffer(vertexDescription, vertexData);
        IBuffer indices = _device.CreateBuffer(indexDescription, indexData);

        ITexture? texture = null;
        TextureData? data = part.Material.BaseColorTexture;
        if (data is not null)
        {
            TextureDescription description = new(data.Width, data.Height, PixelFormat.Rgba8UNormSrgb, TextureUsage.ShaderRead)
            {
                MipLevels = TextureDescription.CountMipLevels(data.Width, data.Height),
                Name = $"{name} base color",
            };
            texture = _device.CreateTexture(description, data.Rgba);
        }

        return new GpuMeshPart(vertices, indices, part.Indices.Length, texture, part.Material.BaseColor);
    }
}
