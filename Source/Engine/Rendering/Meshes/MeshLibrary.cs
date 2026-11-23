using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Loads each model the first time it is drawn and keeps it on the GPU until its file changes on disk and the next draw loads
/// it again. A model is named by its source (Room.glb); the game loads what the content build baked of it (Room.fvmodel).
/// Greybox boxes are made from their names (<see cref="GreyboxMesh"/>).
/// </summary>
internal sealed class MeshLibrary : IDisposable
{
    private readonly IGraphicsDevice _device;
    private readonly IContentManager _content;
    private readonly Dictionary<string, GpuModel> _models = new(ContentManager.PathComparer);

    public MeshLibrary(IGraphicsDevice device, IContentManager content)
    {
        _device = device;
        _content = content;
        _content.Changed += HandleChanged;
    }

    public GpuModel Get(string path)
    {
        if (GreyboxMesh.TryGetSize(path, out Vector3 size))
        {
            return GetGreybox(path, size);
        }

        string builtPath = Path.ChangeExtension(path, ".fvmodel");
        string fullPath = _content.GetFullPath(builtPath);
        if (_models.TryGetValue(fullPath, out GpuModel? loaded))
        {
            return loaded;
        }

        ModelData data;
        using (FileStream stream = File.OpenRead(fullPath))
        {
            data = ModelFile.Read(stream);
        }

        GpuMeshPart[] uploaded = [.. data.Parts.Select(part => Upload(part, path))];
        GpuModel model = new(uploaded, data.Animations);
        _models.Add(fullPath, model);
        return model;
    }

    private GpuModel GetGreybox(string name, Vector3 size)
    {
        if (_models.TryGetValue(name, out GpuModel? made))
        {
            return made;
        }

        ModelData data = GreyboxMesh.Create(size);
        GpuMeshPart[] uploaded = [.. data.Parts.Select(part => Upload(part, name))];
        GpuModel model = new(uploaded, data.Animations);
        _models.Add(name, model);
        return model;
    }

    public void Dispose()
    {
        _content.Changed -= HandleChanged;
        foreach (GpuModel model in _models.Values)
        {
            Release(model);
        }

        _models.Clear();
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
        if (_models.Remove(fullPath, out GpuModel? model))
        {
            Release(model);
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
        DdsImage? image = part.Material.BaseColorTexture;
        if (image is not null)
        {
            TextureDescription description = image.Describe(TextureUsage.ShaderRead) with { Name = $"{name} base color" };
            texture = _device.CreateTexture(description, image.Data);
        }

        return new GpuMeshPart(vertices, part.Vertices.Length, indices, part.Indices.Length, skin, texture, part.Material.BaseColor);
    }
}
