using FlickerVoid.Engine.Builder.Importing;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Builder.Textures;

/// <summary>Compresses an image (PNG, JPEG, TGA or BMP) as sRGB color to BC7 with its full mip chain, in <c>Name.dds</c>.</summary>
public sealed class TextureImporter : IAssetImporter
{
    public string Name => "Texture";

    public int Version => 1;

    public IReadOnlyList<string> Extensions { get; } = [".png", ".jpg", ".jpeg", ".tga", ".bmp"];

    public void Import(AssetImportContext context)
    {
        byte[] encoded = File.ReadAllBytes(context.SourceFullPath);
        DdsImage image = TextureCompressor.Compress(encoded);
        string outputPath = Path.ChangeExtension(context.SourcePath, ".dds");
        string fullOutputPath = context.GetOutputPath(outputPath);
        using FileStream output = File.Create(fullOutputPath);
        image.Write(output);
    }
}
