using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Editor.Assets.Tests.Materials;
using FlickerVoid.Editor.Materials;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Tests.Materials;

/// <summary>Builds the material preview on the test machine's GPU, skipping the test without one.</summary>
internal sealed class MaterialPreviewTestsBuilder : IDisposable
{
    private readonly string _content = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private MaterialPreview? _preview;

    public MaterialPreviewTestsBuilder()
    {
        Directory.CreateDirectory(_content);
    }

    /// <summary>A one-texel texture of the colour in the editor's content folder, built as the builder would.</summary>
    public MaterialPreviewTestsBuilder WithTexture(string path, byte red, byte green, byte blue)
    {
        byte[] texel = [red, green, blue, 255];
        DdsImage image = new(1, 1, PixelFormat.Rgba8UNormSrgb, 1, texel);
        string builtPath = Path.ChangeExtension(path, ".dds");
        using FileStream stream = File.Create(Path.Combine(_content, builtPath));
        image.Write(stream);
        return this;
    }

    public MaterialPreview Build()
    {
        try
        {
            _preview = new MaterialPreview(NullLoggerFactory.Instance, _content);
        }
        catch (GraphicsUnavailableException exception)
        {
            Assert.Skip(exception.Message);
        }

        return _preview;
    }

    /// <summary>The red, green and blue of the pixel at the column and row.</summary>
    public static (byte Red, byte Green, byte Blue) PixelAt(byte[] pixels, int size, int x, int y)
    {
        int offset = ((y * size) + x) * 4;
        return (pixels[offset], pixels[offset + 1], pixels[offset + 2]);
    }

    public void Dispose()
    {
        _preview?.Dispose();
        Directory.Delete(_content, recursive: true);
    }
}
