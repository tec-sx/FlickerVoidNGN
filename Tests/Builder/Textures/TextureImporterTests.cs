using BCnEncoder.Decoder;
using BCnEncoder.Shared;

using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Builder.Tests.Textures;

public sealed class TextureImporterTests : IDisposable
{
    private readonly TextureImporterTestsBuilder _files = new();

    public void Dispose()
    {
        _files.Dispose();
    }

    [Fact]
    public void CompressesAnImageToSrgbBc7WithEveryMipLevel()
    {
        // Left half red, right half blue.
        byte[] rgba = new byte[8 * 4 * 4];
        for (int texel = 0; texel < 32; texel++)
        {
            bool left = texel % 8 < 4;
            rgba[texel * 4] = left ? (byte)255 : (byte)0;
            rgba[texel * 4 + 2] = left ? (byte)0 : (byte)255;
            rgba[texel * 4 + 3] = 255;
        }

        _files.WithImage("Paint.tga", 8, 4, rgba);
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().BeEmpty();
        DdsImage image = _files.ReadOutput("Paint.dds");
        image.Width.Should().Be(8);
        image.Height.Should().Be(4);
        image.Format.Should().Be(PixelFormat.Bc7UNormSrgb);
        image.MipLevels.Should().Be(4);
        byte[] topLevel = image.Data[..32];
        BcDecoder decoder = new();
        ColorRgba32[] top = decoder.DecodeRaw(topLevel, 8, 4, CompressionFormat.Bc7);
        top[0].r.Should().BeInRange(250, 255);
        top[0].b.Should().BeInRange(0, 5);
        top[7].r.Should().BeInRange(0, 5);
        top[7].b.Should().BeInRange(250, 255);
    }

    [Fact]
    public void AFileThatIsNotAnImageFails()
    {
        _files.WithSource("Broken.png", "not an image");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().ContainSingle().Which.Path.Should().Be("Broken.png");
    }
}
