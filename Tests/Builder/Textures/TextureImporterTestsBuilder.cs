using FlickerVoid.Engine.Builder.Textures;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Builder.Tests.Textures;

/// <summary>
/// Builds an asset builder with the texture importer under test, over new temporary source and output folders that are
/// deleted when disposed, and writes images as uncompressed TGA files.
/// </summary>
internal sealed class TextureImporterTestsBuilder : IDisposable
{
    private readonly AssetBuilderTestsBuilder _files = new();

    public TextureImporterTestsBuilder WithImage(string path, int width, int height, byte[] rgba)
    {
        byte[] tga = EncodeTga(width, height, rgba);
        string fullPath = Path.Combine(_files.SourceDirectory, path);
        File.WriteAllBytes(fullPath, tga);
        return this;
    }

    public TextureImporterTestsBuilder WithSource(string path, string text)
    {
        _files.WithSource(path, text);
        return this;
    }

    public AssetBuilder Build() => new([new TextureImporter()]);

    public BuildReport BuildAll(AssetBuilder builder) => _files.BuildAll(builder);

    public DdsImage ReadOutput(string path)
    {
        string fullPath = Path.Combine(_files.OutputDirectory, path);
        using FileStream stream = File.OpenRead(fullPath);
        return DdsImage.Read(stream);
    }

    public void Dispose()
    {
        _files.Dispose();
    }

    /// <summary>An uncompressed 32-bit TGA, rows from the top.</summary>
    private static byte[] EncodeTga(int width, int height, byte[] rgba)
    {
        const int headerSize = 18;
        byte[] tga = new byte[headerSize + rgba.Length];
        tga[2] = 2;
        tga[12] = (byte)width;
        tga[13] = (byte)(width >> 8);
        tga[14] = (byte)height;
        tga[15] = (byte)(height >> 8);
        tga[16] = 32;
        tga[17] = 0x28;
        for (int texel = 0; texel < rgba.Length / 4; texel++)
        {
            int source = texel * 4;
            int destination = headerSize + source;
            tga[destination] = rgba[source + 2];
            tga[destination + 1] = rgba[source + 1];
            tga[destination + 2] = rgba[source];
            tga[destination + 3] = rgba[source + 3];
        }

        return tga;
    }
}
