using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// Loads each texture the first time it is used and keeps it on the GPU until its file changes on disk and the next use
/// loads it again. A texture is named by its source image (Paper.png); the game loads what the content build made of it
/// (Paper.dds).
/// </summary>
internal sealed class TextureLibrary : IDisposable
{
    private readonly IGraphicsDevice _device;
    private readonly IContentManager _content;
    private readonly Dictionary<string, ITexture> _textures = new(ContentManager.PathComparer);

    public TextureLibrary(IGraphicsDevice device, IContentManager content)
    {
        _device = device;
        _content = content;
        _content.Changed += HandleChanged;
    }

    /// <summary>The file the content build makes of a texture's source image.</summary>
    public static string GetBuiltPath(string path)
    {
        return Path.ChangeExtension(path, ".dds");
    }

    public ITexture Get(string path)
    {
        string builtPath = GetBuiltPath(path);
        string fullPath = _content.GetFullPath(builtPath);
        if (_textures.TryGetValue(fullPath, out ITexture? loaded))
        {
            return loaded;
        }

        DdsImage image;
        using (FileStream stream = File.OpenRead(fullPath))
        {
            image = DdsImage.Read(stream);
        }

        TextureDescription description = image.Describe(TextureUsage.ShaderRead) with { Name = path };
        ITexture texture = _device.CreateTexture(description, image.Data);
        _textures.Add(fullPath, texture);
        return texture;
    }

    public void Dispose()
    {
        _content.Changed -= HandleChanged;
        foreach (ITexture texture in _textures.Values)
        {
            texture.Dispose();
        }

        _textures.Clear();
    }

    // The device releases the old texture once the GPU is done with it.
    private void HandleChanged(string fullPath)
    {
        if (_textures.Remove(fullPath, out ITexture? texture))
        {
            texture.Dispose();
        }
    }
}
