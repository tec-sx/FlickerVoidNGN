using BCnEncoder.Encoder;
using BCnEncoder.Shared;

using FlickerVoid.Engine.Graphics.Textures;

using StbImageSharp;

using EncoderPixelFormat = BCnEncoder.Encoder.PixelFormat;
using PixelFormat = FlickerVoid.Engine.Graphics.Textures.PixelFormat;

namespace FlickerVoid.Tools.Builder.Textures;

/// <summary>Decodes images (PNG, JPEG, TGA, BMP) and compresses them as sRGB color to BC7 with their full mip chain.</summary>
public static class TextureCompressor
{
    public static DdsImage Compress(byte[] encodedImage)
    {
        ImageResult decoded = ImageResult.FromMemory(encodedImage, ColorComponents.RedGreenBlueAlpha);
        BcEncoder encoder = new(CompressionFormat.Bc7);
        encoder.OutputOptions.GenerateMipMaps = true;
        encoder.OutputOptions.Quality = CompressionQuality.Balanced;
        byte[][] levels = encoder.EncodeToRawBytes(decoded.Data, decoded.Width, decoded.Height, EncoderPixelFormat.Rgba32);
        byte[] data = [.. levels.SelectMany(static level => level)];
        return new DdsImage(decoded.Width, decoded.Height, PixelFormat.Bc7UNormSrgb, levels.Length, data);
    }
}
