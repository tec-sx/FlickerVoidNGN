using System.Text.Json;

using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Tools.Builder.Importing;

using StbImageSharp;

namespace FlickerVoid.Tools.Builder.Textures;

/// <summary>
/// Makes an image (PNG, JPEG, TGA or BMP) an sRGB texture with its full mip chain, in <c>Name.dds</c>: as its
/// <c>Name.png.fvtexture</c> settings say, when it has them (cropped, shrunk, tinted, BC7 or uncompressed, or a 16-bit linear
/// mask), else whole as BC7.
/// </summary>
public sealed class TextureImporter : IAssetImporter
{
    public string Name => "Texture";

    public int Version => 3;

    public IReadOnlyList<string> Extensions { get; } = [".png", ".jpg", ".jpeg", ".tga", ".bmp"];

    public void Import(AssetImportContext context)
    {
        TextureSettings settings = ReadSettings(context);
        byte[] encoded = File.ReadAllBytes(context.SourceFullPath);
        if (settings.Mask)
        {
            DdsImage mask = MaskDecoder.Decode(encoded);
            Write(context, mask);
            return;
        }

        ImageResult decoded = ImageResult.FromMemory(encoded, ColorComponents.RedGreenBlueAlpha);
        TextureEdits source = new(decoded.Data, decoded.Width, decoded.Height);
        TextureEdits edited = source.Apply(settings);
        DdsImage image;
        if (settings.Compression == TextureCompression.None)
        {
            byte[] data = edited.WithMips(out int levels);
            image = new DdsImage(edited.Width, edited.Height, PixelFormat.Rgba8UNormSrgb, levels, data);
        }
        else
        {
            image = TextureCompressor.Compress(edited.Rgba, edited.Width, edited.Height, PixelFormat.Bc7UNormSrgb);
        }

        Write(context, image);
    }

    private static void Write(AssetImportContext context, DdsImage image)
    {
        string outputPath = Path.ChangeExtension(context.SourcePath, ".dds");
        string fullOutputPath = context.GetOutputPath(outputPath);
        using FileStream output = File.Create(fullOutputPath);
        image.Write(output);
    }

    /// <summary>The image's settings, recorded as a dependency whether or not they exist, so adding them builds it again.</summary>
    private static TextureSettings ReadSettings(AssetImportContext context)
    {
        string fullPath = TextureSettings.GetPath(context.SourceFullPath);
        context.AddDependency(fullPath);
        if (!File.Exists(fullPath))
        {
            return new TextureSettings();
        }

        using FileStream stream = File.OpenRead(fullPath);
        TextureSettings? settings = JsonSerializer.Deserialize(stream, RenderingJsonContext.Default.TextureSettings);
        return settings ?? new TextureSettings();
    }
}
