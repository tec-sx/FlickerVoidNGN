using BCnEncoder.Encoder;
using BCnEncoder.Shared;

using FlickerVoid.Engine.Builder.Importing;
using FlickerVoid.Engine.Graphics.Textures;

using StbImageSharp;

using EncoderPixelFormat = BCnEncoder.Encoder.PixelFormat;
using PixelFormat = FlickerVoid.Engine.Graphics.Textures.PixelFormat;

namespace FlickerVoid.Engine.Builder.Textures;

/// <summary>Compresses an image (PNG, JPEG, TGA or BMP) as sRGB color to BC7 with its full mip chain, in <c>Name.dds</c>.</summary>
public sealed class TextureImporter : IAssetImporter
{
    public string Name => "Texture";

    public int Version => 1;

    public IReadOnlyList<string> Extensions { get; } = [".png", ".jpg", ".jpeg", ".tga", ".bmp"];

    public void Import(AssetImportContext context)
    {
        ImageResult decoded;
        using (FileStream stream = File.OpenRead(context.SourceFullPath))
        {
            decoded = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        }

        BcEncoder encoder = new(CompressionFormat.Bc7);
        encoder.OutputOptions.GenerateMipMaps = true;
        encoder.OutputOptions.Quality = CompressionQuality.Balanced;
        byte[][] levels = encoder.EncodeToRawBytes(decoded.Data, decoded.Width, decoded.Height, EncoderPixelFormat.Rgba32);
        byte[] data = [.. levels.SelectMany(static level => level)];
        DdsImage image = new(decoded.Width, decoded.Height, PixelFormat.Bc7UNormSrgb, levels.Length, data);

        string outputPath = Path.ChangeExtension(context.SourcePath, ".dds");
        string fullOutputPath = context.GetOutputPath(outputPath);
        using FileStream output = File.Create(fullOutputPath);
        image.Write(output);
    }
}
