using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Runtime.Content;

using StbImageSharp;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// Loads each image file (PNG, JPEG, TGA, BMP...) the first time it is used and keeps it on the GPU as sRGB color, until the
/// file changes on disk and the next use loads it again.
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

    public ITexture Get(string path)
    {
        string fullPath = _content.GetFullPath(path);
        if (_textures.TryGetValue(fullPath, out ITexture? loaded))
        {
            return loaded;
        }

        using FileStream stream = File.OpenRead(fullPath);
        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        TextureDescription description = new(image.Width, image.Height, PixelFormat.Rgba8UNormSrgb, TextureUsage.ShaderRead) { Name = path };
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
