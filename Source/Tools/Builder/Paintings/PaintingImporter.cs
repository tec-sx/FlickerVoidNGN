using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Tools.Builder.Importing;
using FlickerVoid.Tools.Builder.Textures;

namespace FlickerVoid.Tools.Builder.Paintings;

/// <summary>
/// Turns a Rebelle painting (<c>Name.reb</c>) into a texture, <c>Name.dds</c>: its visible layers laid over each other, as
/// sRGB BC7 with alpha keeping how thick the paint is, so the paper shows through where it is thin. The paper is left out.
/// </summary>
public sealed class PaintingImporter : IAssetImporter
{
    public string Name => "Painting";

    public int Version => 1;

    public IReadOnlyList<string> Extensions { get; } = [".reb"];

    public void Import(AssetImportContext context)
    {
        byte[] rgba;
        int width;
        int height;
        using (FileStream source = File.OpenRead(context.SourceFullPath))
        using (RebellePainting painting = RebellePainting.Open(source))
        {
            rgba = painting.Composite();
            width = painting.Width;
            height = painting.Height;
        }

        DdsImage image = TextureCompressor.Compress(rgba, width, height);
        string outputPath = Path.ChangeExtension(context.SourcePath, ".dds");
        string fullOutputPath = context.GetOutputPath(outputPath);
        using FileStream output = File.Create(fullOutputPath);
        image.Write(output);
    }
}
