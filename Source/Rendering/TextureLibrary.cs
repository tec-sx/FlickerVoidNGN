using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Runtime.Content;

using StbImageSharp;

namespace FlickerVoid.Engine.Rendering;

/// <summary>Loads each image file (PNG, JPEG, TGA, BMP...) the first time it is used and keeps it on the GPU as sRGB color.</summary>
internal sealed class TextureLibrary : IDisposable
{
    private readonly IGraphicsDevice _device;
    private readonly IContentManager _content;
    private readonly Dictionary<string, ITexture> _textures = [];

    public TextureLibrary(IGraphicsDevice device, IContentManager content)
    {
        _device = device;
        _content = content;
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
        foreach (ITexture texture in _textures.Values)
        {
            texture.Dispose();
        }

        _textures.Clear();
    }
}
