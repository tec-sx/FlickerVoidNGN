using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Makes each mesh the first time it is drawn, through the renderer's <see cref="MeshSources"/>, and keeps it on the GPU until the
/// file it was made from changes on disk and the next draw makes it again.
/// </summary>
internal sealed class MeshLibrary : IDisposable
{
    private readonly IGraphicsDevice _device;
    private readonly IContentManager _content;
    private readonly Dictionary<string, Made> _models = new(ContentManager.PathComparer);
    private MeshSources? _sources;

    public MeshLibrary(IGraphicsDevice device, IContentManager content)
    {
        _device = device;
        _content = content;
        _content.Changed += HandleChanged;
    }

    /// <summary>The mesh as <paramref name="sources"/> make it; other sources than last time make every mesh again.</summary>
    public GpuModel Get(string mesh, MeshSources sources)
    {
        if (!ReferenceEquals(sources, _sources))
        {
            ReleaseAll();
            _sources = sources;
        }

        if (_models.TryGetValue(mesh, out Made? loaded))
        {
            return loaded.Model;
        }

        MadeMesh? made = sources.Make(mesh, _content);
        if (made is null)
        {
            string builtPath = Path.ChangeExtension(mesh, ".fvmodel");
            string fullPath = _content.GetFullPath(builtPath);
            throw new FileNotFoundException($"No mesh source makes {mesh}, and the content build baked no model of it.", fullPath);
        }

        string? file = null;
        if (made.File is not null)
        {
            file = _content.GetFullPath(made.File);
        }

        GpuMeshPart[] uploaded = [.. made.Model.Parts.Select(part => Upload(part, mesh))];
        GpuModel model = new(uploaded, made.Model.Animations);
        _models.Add(mesh, new Made(model, file));
        return model;
    }

    public void Dispose()
    {
        _content.Changed -= HandleChanged;
        ReleaseAll();
    }

    private void ReleaseAll()
    {
        foreach (Made made in _models.Values)
        {
            Release(made.Model);
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
        List<string> changed = [];
        foreach ((string mesh, Made made) in _models)
        {
            if (made.File is not null && ContentManager.PathComparer.Equals(made.File, fullPath))
            {
                changed.Add(mesh);
            }
        }

        foreach (string mesh in changed)
        {
            if (_models.Remove(mesh, out Made? made))
            {
                Release(made.Model);
            }
        }
    }

    /// <summary>A mesh on the GPU and the full path of the file it was made from, if any.</summary>
    private sealed record Made(GpuModel Model, string? File);

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

        Bounds bounds = Bounds.Empty;
        foreach (MeshVertex vertex in part.Vertices)
        {
            bounds = bounds.Include(vertex.Position);
        }

        return new GpuMeshPart(vertices, part.Vertices.Length, indices, part.Indices.Length, skin, texture, part.Material.BaseColor, bounds);
    }
}
