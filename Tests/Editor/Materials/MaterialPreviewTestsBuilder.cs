using FlickerVoid.Engine.Editor.Materials;
using FlickerVoid.Engine.Graphics.Devices;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Editor.Tests.Materials;

/// <summary>Builds the material preview on the test machine's GPU, skipping the test without one.</summary>
internal sealed class MaterialPreviewTestsBuilder : IDisposable
{
    private MaterialPreview? _preview;

    public MaterialPreview Build()
    {
        try
        {
            _preview = new MaterialPreview(NullLoggerFactory.Instance);
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
    }
}
