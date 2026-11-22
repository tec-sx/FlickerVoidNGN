using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Tools.Builder.Importing;
using FlickerVoid.Tools.Builder.Textures;

namespace FlickerVoid.Tools.Builder.Paintings;

/// <summary>
/// Turns a Rebelle painting (<c>Name.reb</c>) into a texture, <c>Name.dds</c>: its visible layers laid over each other, as
/// sRGB BC7 with alpha keeping how thick the paint is, so the paper shows through where it is thin, and its impasto, when it
/// has any, into <c>Name.relief.dds</c>: how high the paint stands, grey linear BC7. The paper is left out.
/// </summary>
public sealed class PaintingImporter : IAssetImporter
{
    public string Name => "Painting";

    public int Version => 2;

    public IReadOnlyList<string> Extensions { get; } = [".reb"];

    public void Import(AssetImportContext context)
    {
        byte[] rgba;
        byte[]? relief;
        int width;
        int height;
        using (FileStream source = File.OpenRead(context.SourceFullPath))
        using (RebellePainting painting = RebellePainting.Open(source))
        {
            rgba = painting.Composite();
            relief = painting.CompositeRelief();
            width = painting.Width;
            height = painting.Height;
        }

        DdsImage image = TextureCompressor.Compress(rgba, width, height, PixelFormat.Bc7UNormSrgb);
        Write(context, ".dds", image);
        if (relief is not null)
        {
            DdsImage reliefImage = TextureCompressor.Compress(relief, width, height, PixelFormat.Bc7UNorm);
            Write(context, ".relief.dds", reliefImage);
        }
    }

    private static void Write(AssetImportContext context, string extension, DdsImage image)
    {
        string outputPath = Path.ChangeExtension(context.SourcePath, extension);
        string fullOutputPath = context.GetOutputPath(outputPath);
        using FileStream output = File.Create(fullOutputPath);
        image.Write(output);
    }
}
