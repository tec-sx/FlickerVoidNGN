using BCnEncoder.Encoder;
using BCnEncoder.Shared;

using FlickerVoid.Engine.Graphics.Textures;

using StbImageSharp;

using EncoderPixelFormat = BCnEncoder.Encoder.PixelFormat;
using PixelFormat = FlickerVoid.Engine.Graphics.Textures.PixelFormat;

namespace FlickerVoid.Tools.Builder.Textures;

/// <summary>Decodes images (PNG, JPEG, TGA, BMP), or takes raw texels, and compresses them as sRGB color to BC7 with their full mip chain.</summary>
public static class TextureCompressor
{
    public static DdsImage Compress(byte[] encodedImage)
    {
        ImageResult decoded = ImageResult.FromMemory(encodedImage, ColorComponents.RedGreenBlueAlpha);
        return Compress(decoded.Data, decoded.Width, decoded.Height);
    }

    /// <summary>Compresses RGBA texels, rows from the top.</summary>
    public static DdsImage Compress(byte[] rgba, int width, int height)
    {
        BcEncoder encoder = new(CompressionFormat.Bc7);
        encoder.OutputOptions.GenerateMipMaps = true;
        encoder.OutputOptions.Quality = CompressionQuality.Balanced;
        byte[][] levels = encoder.EncodeToRawBytes(rgba, width, height, EncoderPixelFormat.Rgba32);
        byte[] data = [.. levels.SelectMany(static level => level)];
        return new DdsImage(width, height, PixelFormat.Bc7UNormSrgb, levels.Length, data);
    }
}
