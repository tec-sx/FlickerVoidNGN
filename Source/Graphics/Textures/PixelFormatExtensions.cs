using FlickerVoid.Engine.Graphics.Devices;

namespace FlickerVoid.Engine.Graphics.Textures;

/// <summary>Sizes and kinds of <see cref="PixelFormat"/>s.</summary>
public static class PixelFormatExtensions
{
    public static int GetBytesPerPixel(this PixelFormat format)
    {
        switch (format)
        {
            case PixelFormat.Rgba32Float:
                return 16;
            case PixelFormat.Rgba16Float:
                return 8;
            case PixelFormat.Rg11B10Float:
            case PixelFormat.Rgba8UNorm:
            case PixelFormat.Rgba8UNormSrgb:
            case PixelFormat.Rg16Float:
            case PixelFormat.Depth32Float:
            case PixelFormat.R32Float:
            case PixelFormat.Depth24UNormStencil8:
            case PixelFormat.Bgra8UNorm:
            case PixelFormat.Bgra8UNormSrgb:
                return 4;
            case PixelFormat.R16Float:
            case PixelFormat.Depth16UNorm:
                return 2;
            case PixelFormat.R8UNorm:
                return 1;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    public static bool IsDepth(this PixelFormat format)
    {
        return format is PixelFormat.Depth32Float or PixelFormat.Depth24UNormStencil8 or PixelFormat.Depth16UNorm;
    }

    public static bool HasStencil(this PixelFormat format) => format == PixelFormat.Depth24UNormStencil8;

    /// <summary>Bytes per row in a buffer-texture copy, padded to <see cref="GraphicsLimits.TextureRowPitchAlignment"/>.</summary>
    public static int GetCopyRowPitch(this PixelFormat format, int width)
    {
        int rowSize = format.GetBytesPerPixel() * width;
        int alignment = GraphicsLimits.TextureRowPitchAlignment;
        return (rowSize + alignment - 1) / alignment * alignment;
    }
}

