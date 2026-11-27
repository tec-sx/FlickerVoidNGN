using System.Runtime.InteropServices;

using BCnEncoder.Decoder;
using BCnEncoder.Shared;

using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Tools.Builder.Tests.Textures;

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
    public void KeepsAMasksSixteenBitGreysLinearInRed()
    {
        ushort[] greys = [0, 1000, 32768, 65535];
        _files.WithGreyPng16("Sky.png", 4, 1, greys);
        _files.WithSource("Sky.png.fvtexture", """{ "mask": true }""");

        DdsImage image = BuildOne("Sky.dds");

        image.Format.Should().Be(PixelFormat.Rgba16Float);
        image.MipLevels.Should().Be(1);
        System.Half[] texels = MemoryMarshal.Cast<byte, System.Half>(image.Data).ToArray();
        ((float)texels[4]).Should().BeApproximately(1000 / 65535f, 2e-5f, "a 16-bit step between greys survives");
        ((float)texels[8]).Should().BeApproximately(0.5f, 1e-3f, "a mask is not turned from sRGB");
        ((float)texels[12]).Should().Be(1);
        ((float)texels[13]).Should().Be(0, "a grey mask fills only red");
        ((float)texels[14]).Should().Be(0);
        ((float)texels[15]).Should().Be(1);
    }

    [Fact]
    public void CropsTheImageToThePartItsSettingsKeep()
    {
        _files.WithImage("Paint.tga", 8, 4, HalfRedHalfBlue());
        _files.WithSource("Paint.tga.fvtexture", """{ "cropX": 4, "cropWidth": 4 }""");

        DdsImage image = BuildOne();

        image.Width.Should().Be(4);
        image.Height.Should().Be(4);
        ColorRgba32[] top = Decode(image);
        top.Should().OnlyContain(static texel => texel.b > 250 && texel.r < 5, "only the blue half is kept");
    }

    [Fact]
    public void HalvesTheImageUntilItFitsTheSizeLimit()
    {
        _files.WithImage("Paint.tga", 8, 4, HalfRedHalfBlue());
        _files.WithSource("Paint.tga.fvtexture", """{ "maxSize": 3 }""");

        DdsImage image = BuildOne();

        image.Width.Should().Be(2);
        image.Height.Should().Be(1);
    }

    [Fact]
    public void TintsTheImage()
    {
        _files.WithImage("Paint.tga", 8, 4, HalfRedHalfBlue());
        _files.WithSource("Paint.tga.fvtexture", """{ "tint": [0.5, 1, 1], "compression": "None" }""");

        DdsImage image = BuildOne();

        image.Data[0].Should().BeInRange(126, 129, "red is halved");
        image.Data[2].Should().Be(0);
    }

    [Fact]
    public void RaisesAReliefFromTheImagesBrightness()
    {
        _files.WithImage("Paint.tga", 8, 4, HalfWhiteHalfBlack());
        _files.WithSource("Paint.tga.fvtexture", """{ "relief": true }""");

        DdsImage relief = BuildOne("Paint.relief.dds");

        relief.Format.Should().Be(PixelFormat.Bc7UNorm, "a relief is heights, not colours");
        ColorRgba32[] texels = Decode(relief);
        texels[0].r.Should().BeGreaterThan(250, "white paint stands highest");
        texels[7].r.Should().BeLessThan(5, "black paint lies flat");
    }

    [Fact]
    public void RaisesNoReliefUnlessTheSettingsAskForOne()
    {
        _files.WithImage("Paint.tga", 8, 4, HalfWhiteHalfBlack());

        BuildOne();

        File.Exists(_files.OutputPath("Paint.relief.dds")).Should().BeFalse();
    }

    [Fact]
    public void KeepsEveryTexelWhenUncompressed()
    {
        _files.WithImage("Paint.tga", 8, 4, HalfRedHalfBlue());
        _files.WithSource("Paint.tga.fvtexture", """{ "compression": "None" }""");

        DdsImage image = BuildOne();

        image.Format.Should().Be(PixelFormat.Rgba8UNormSrgb);
        image.MipLevels.Should().Be(4);
        image.Data[..4].Should().Equal(255, 0, 0, 255);
        image.Data[28..32].Should().Equal(0, 0, 255, 255);
    }

    [Fact]
    public void BuildsTheImageAgainWhenItsSettingsChange()
    {
        _files.WithImage("Paint.tga", 8, 4, HalfRedHalfBlue());
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);
        _files.WithSource("Paint.tga.fvtexture", """{ "maxSize": 4 }""");

        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Contain("Paint.tga");
        _files.ReadOutput("Paint.dds").Width.Should().Be(4);
    }

    [Fact]
    public void AFileThatIsNotAnImageFails()
    {
        _files.WithSource("Broken.png", "not an image");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().ContainSingle().Which.Path.Should().Be("Broken.png");
    }

    /// <summary>An 8 by 4 image, its left half red and its right half blue.</summary>
    private static byte[] HalfWhiteHalfBlack()
    {
        byte[] rgba = new byte[8 * 4 * 4];
        for (int texel = 0; texel < 32; texel++)
        {
            byte grey = texel % 8 < 4 ? (byte)255 : (byte)0;
            rgba[texel * 4] = grey;
            rgba[(texel * 4) + 1] = grey;
            rgba[(texel * 4) + 2] = grey;
            rgba[(texel * 4) + 3] = 255;
        }

        return rgba;
    }

    private static byte[] HalfRedHalfBlue()
    {
        byte[] rgba = new byte[8 * 4 * 4];
        for (int texel = 0; texel < 32; texel++)
        {
            bool left = texel % 8 < 4;
            rgba[texel * 4] = left ? (byte)255 : (byte)0;
            rgba[texel * 4 + 2] = left ? (byte)0 : (byte)255;
            rgba[texel * 4 + 3] = 255;
        }

        return rgba;
    }

    private DdsImage BuildOne(string output = "Paint.dds")
    {
        AssetBuilder builder = _files.Build();
        BuildReport report = _files.BuildAll(builder);
        report.Failed.Should().BeEmpty();
        return _files.ReadOutput(output);
    }

    private static ColorRgba32[] Decode(DdsImage image)
    {
        int size = image.Width * image.Height;
        byte[] topLevel = image.Data[..size];
        BcDecoder decoder = new();
        return decoder.DecodeRaw(topLevel, image.Width, image.Height, CompressionFormat.Bc7);
    }
}
