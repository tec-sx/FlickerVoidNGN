using System.Text.Json;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// Packs each material file the first time it is drawn into a GPU buffer of the scene shader's parameters, and keeps it until
/// the file or a texture it names changes on disk; mesh parts without a material share one buffer of the shader's defaults.
/// </summary>
internal sealed class MaterialLibrary : IDisposable
{
    /// <summary>The scene shader's parameter below which paint is a hole (<see cref="GpuMaterial.Cutout"/>).</summary>
    public const string CutoffParameter = "cutoff";

    private readonly IGraphicsDevice _device;
    private readonly IContentManager _content;
    private readonly IReadOnlyList<ShaderParameter> _parameters;
    private readonly Dictionary<string, GpuMaterial> _materials = new(ContentManager.PathComparer);
    private readonly Dictionary<string, List<string>> _textureUsers = new(ContentManager.PathComparer);

    public MaterialLibrary(IGraphicsDevice device, IContentManager content, IReadOnlyList<ShaderParameter> parameters)
    {
        _device = device;
        _content = content;
        _parameters = parameters;
        byte[] defaults = MaterialLayout.Pack(parameters, null);
        IBuffer buffer = Upload(defaults, "Default material");
        Default = new GpuMaterial(buffer, CutsOut(null));
        _content.Changed += HandleChanged;
    }

    public GpuMaterial Default { get; }

    /// <summary>The material file's buffer, with its textures loaded from <paramref name="textures"/>, or the defaults' for a part without one.</summary>
    public GpuMaterial Get(string? path, TextureLibrary textures)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Default;
        }

        string fullPath = _content.GetFullPath(path);
        if (_materials.TryGetValue(fullPath, out GpuMaterial loaded))
        {
            return loaded;
        }

        MaterialFile material = _content.Load(path, RenderingJsonContext.Default.MaterialFile);
        byte[] packed = MaterialLayout.Pack(_parameters, material, texturePath =>
        {
            string builtPath = TextureLibrary.GetBuiltPath(texturePath);
            string reliefPath = TextureLibrary.GetReliefPath(texturePath);
            AddTextureUser(builtPath, fullPath);
            AddTextureUser(reliefPath, fullPath);
            ITexture image = textures.Get(texturePath);
            ITexture? relief = textures.GetRelief(texturePath);
            uint reliefIndex = relief?.BindlessIndex ?? MaterialLayout.NoTexture;
            return new MaterialTexture(image.BindlessIndex, reliefIndex);
        });
        IBuffer buffer = Upload(packed, path);
        GpuMaterial made = new(buffer, CutsOut(material));
        _materials.Add(fullPath, made);
        return made;
    }

    public void Dispose()
    {
        _content.Changed -= HandleChanged;
        foreach (GpuMaterial material in _materials.Values)
        {
            material.Parameters.Dispose();
        }

        _materials.Clear();
        Default.Parameters.Dispose();
    }

    /// <summary>Whether the material, or the shader's defaults where it sets none, cuts holes where paint is thin.</summary>
    private bool CutsOut(MaterialFile? material)
    {
        ShaderParameter? cutoff = _parameters.FirstOrDefault(static parameter => parameter.Name == CutoffParameter);
        if (cutoff is null)
        {
            return false;
        }

        JsonElement value = cutoff.Default;
        if (material is not null && material.Parameters.TryGetValue(CutoffParameter, out JsonElement set))
        {
            value = set;
        }

        return value.ValueKind == JsonValueKind.Number && value.GetSingle() > 0;
    }

    private IBuffer Upload(byte[] packed, string name)
    {
        BufferDescription description = new(packed.Length, BufferUsage.ShaderRead) { Name = name };
        return _device.CreateBuffer(description, packed);
    }

    private void AddTextureUser(string texturePath, string materialFullPath)
    {
        string textureFullPath = _content.GetFullPath(texturePath);
        if (!_textureUsers.TryGetValue(textureFullPath, out List<string>? users))
        {
            users = [];
            _textureUsers.Add(textureFullPath, users);
        }

        users.Add(materialFullPath);
    }

    // A changed texture loads again under a new index, so the materials naming it are packed again too.
    // The device releases the old buffers once the GPU is done with them.
    private void HandleChanged(string fullPath)
    {
        if (_textureUsers.Remove(fullPath, out List<string>? users))
        {
            foreach (string user in users)
            {
                Forget(user);
            }
        }

        Forget(fullPath);
    }

    private void Forget(string fullPath)
    {
        if (_materials.Remove(fullPath, out GpuMaterial material))
        {
            material.Parameters.Dispose();
        }
    }
}
