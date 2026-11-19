using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Imports each glTF model the first time it is drawn and keeps it on the GPU, its textures with full mip chains, until the
/// model or a buffer or image file it reads changes on disk and the next draw imports it again.
/// </summary>
internal sealed class MeshLibrary : IDisposable
{
    private readonly IGraphicsDevice _device;
    private readonly IContentManager _content;
    private readonly Dictionary<string, LoadedModel> _meshes = new(ContentManager.PathComparer);
    private readonly List<string> _stale = [];

    public MeshLibrary(IGraphicsDevice device, IContentManager content)
    {
        _device = device;
        _content = content;
        _content.Changed += HandleChanged;
    }

    public GpuModel Get(string path)
    {
        string fullPath = _content.GetFullPath(path);
        if (_meshes.TryGetValue(fullPath, out LoadedModel? loaded))
        {
            return loaded.Model;
        }

        ModelData data = GltfImporter.Load(fullPath);
        IReadOnlyList<string> files = GltfImporter.FindFiles(fullPath);
        GpuMeshPart[] uploaded = [.. data.Parts.Select(part => Upload(part, path))];
        GpuModel model = new(uploaded, data.Animations);
        _meshes.Add(fullPath, new LoadedModel(model, files));
        return model;
    }

    public void Dispose()
    {
        _content.Changed -= HandleChanged;
        foreach (LoadedModel loaded in _meshes.Values)
        {
            Release(loaded.Model);
        }

        _meshes.Clear();
    }

    // The device releases the old buffers and textures once the GPU is done with them.
    private static void Release(GpuModel model)
    {
        foreach (GpuMeshPart part in model.Parts)
        {
            part.Dispose();
        }
    }

    private void HandleChanged(string fullPath)
    {
        _stale.Clear();
        foreach ((string modelPath, LoadedModel loaded) in _meshes)
        {
            if (loaded.Files.Contains(fullPath, ContentManager.PathComparer))
            {
                _stale.Add(modelPath);
            }
        }

        foreach (string modelPath in _stale)
        {
            _meshes.Remove(modelPath, out LoadedModel? loaded);
            Release(loaded!.Model);
        }
    }

    private GpuMeshPart Upload(MeshPart part, string name)
    {
        byte[] vertexData = MemoryMarshal.AsBytes(part.Vertices.AsSpan()).ToArray();
        byte[] indexData = MemoryMarshal.AsBytes(part.Indices.AsSpan()).ToArray();
        BufferDescription vertexDescription = new(vertexData.Length, BufferUsage.ShaderRead) { Name = $"{name} vertices" };
        BufferDescription indexDescription = new(indexData.Length, BufferUsage.Index) { Name = $"{name} indices" };
        IBuffer vertices = _device.CreateBuffer(vertexDescription, vertexData);
        IBuffer indices = _device.CreateBuffer(indexDescription, indexData);

        IBuffer? skin = null;
        if (part.Skin is not null)
        {
            byte[] skinData = MemoryMarshal.AsBytes(part.Skin.AsSpan()).ToArray();
            BufferDescription skinDescription = new(skinData.Length, BufferUsage.ShaderRead) { Name = $"{name} skin" };
            skin = _device.CreateBuffer(skinDescription, skinData);
        }

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

        return new GpuMeshPart(vertices, part.Vertices.Length, indices, part.Indices.Length, skin, texture, part.Material.BaseColor);
    }

    /// <summary>A model on the GPU and the full paths of the files it was read from.</summary>
    private sealed record LoadedModel(GpuModel Model, IReadOnlyList<string> Files);
}
