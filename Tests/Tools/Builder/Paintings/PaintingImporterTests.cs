using BCnEncoder.Decoder;
using BCnEncoder.Shared;

using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Tools.Builder.Tests.Paintings;

public sealed class PaintingImporterTests : IDisposable
{
    private readonly PaintingImporterTestsBuilder _files = new();

    public void Dispose()
    {
        _files.Dispose();
    }

    [Fact]
    public void KeepsThePaintsColourAndItsThicknessInAlpha()
    {
        // Solid red, then a thin blue wash.
        _files.WithLayer([255, 0, 0, 255, 0, 0, 255, 128]).WritePainting("Wall.reb");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().BeEmpty();
        DdsImage image = _files.ReadOutput("Wall.dds");
        image.Width.Should().Be(2);
        image.Height.Should().Be(1);
        image.Format.Should().Be(PixelFormat.Bc7UNormSrgb);
        ColorRgba32[] texels = DecodeTop(image);
        texels[0].r.Should().BeInRange(250, 255);
        texels[0].a.Should().BeInRange(250, 255);
        texels[1].b.Should().BeInRange(250, 255);
        texels[1].a.Should().BeInRange(123, 133);
    }

    [Fact]
    public void LaysEachVisibleLayerOverTheOnesBeforeItAtItsOpacity()
    {
        _files.WithLayer([255, 0, 0, 255, 0, 0, 0, 0])
            .WithLayer([0, 255, 0, 255, 0, 255, 0, 255], visible: false)
            .WithLayer([0, 0, 255, 255, 0, 0, 255, 255], opacity: 0.5f)
            .WritePainting("Wall.reb");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().BeEmpty();
        ColorRgba32[] texels = DecodeTop(_files.ReadOutput("Wall.dds"));
        texels[0].r.Should().BeInRange(122, 133);
        texels[0].g.Should().BeInRange(0, 5);
        texels[0].b.Should().BeInRange(122, 133);
        texels[0].a.Should().BeInRange(250, 255);
        texels[1].b.Should().BeInRange(250, 255);
        texels[1].a.Should().BeInRange(122, 133);
    }

    [Fact]
    public void PlacesALayerCroppedToItsPaintAtItsOffset()
    {
        _files.WithCanvas(4, 1).WithLayer([0, 255, 0, 255], x: 2, y: 0, width: 1, height: 1).WritePainting("Wall.reb");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().BeEmpty();
        DdsImage image = _files.ReadOutput("Wall.dds");
        image.Width.Should().Be(4);
        ColorRgba32[] texels = DecodeTop(image, 4);
        texels[1].a.Should().BeInRange(0, 5);
        texels[2].g.Should().BeInRange(250, 255);
        texels[2].a.Should().BeInRange(250, 255);
        texels[3].a.Should().BeInRange(0, 5);
    }

    [Fact]
    public void WritesTheImpastoAsAReliefBeside()
    {
        _files.WithLayer(new byte[2 * 4]).WithRelief([0, 4096]).WritePainting("Wall.reb");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().BeEmpty();
        DdsImage relief = _files.ReadOutput("Wall.relief.dds");
        relief.Format.Should().Be(PixelFormat.Bc7UNorm);
        ColorRgba32[] texels = DecodeTop(relief);
        texels[0].r.Should().BeInRange(0, 5);
        texels[1].r.Should().BeInRange(122, 133);
    }

    [Fact]
    public void StacksTheImpastoOfTheVisibleLayersAtTheirOpacityInTheirBoxes()
    {
        _files.WithCanvas(4, 1)
            .WithLayer(new byte[4 * 4]).WithRelief([2048, 2048, 2048, 2048])
            .WithLayer(new byte[4 * 4], visible: false).WithRelief([8192, 8192, 8192, 8192])
            .WithLayer(new byte[4], opacity: 0.5f, x: 2, width: 1, height: 1).WithRelief([4096], x: 2, width: 1, height: 1)
            .WritePainting("Wall.reb");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().BeEmpty();
        ColorRgba32[] texels = DecodeTop(_files.ReadOutput("Wall.relief.dds"), 4);
        texels[1].r.Should().BeInRange(59, 69);
        texels[2].r.Should().BeInRange(122, 133);
    }

    [Fact]
    public void WritesNoReliefForAPaintingWithoutImpasto()
    {
        _files.WithLayer(new byte[2 * 4]).WritePainting("Wall.reb");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().BeEmpty();
        _files.HasOutput("Wall.relief.dds").Should().BeFalse();
    }

    [Fact]
    public void RoundsACanvasSizedInPrintUnitsToWholeTexels()
    {
        _files.WithCanvas(3, 2, "width='2.6' height='1.5'").WithLayer(new byte[3 * 2 * 4]).WritePainting("Wall.reb");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().BeEmpty();
        DdsImage image = _files.ReadOutput("Wall.dds");
        image.Width.Should().Be(3);
        image.Height.Should().Be(2);
    }

    [Fact]
    public void AFileThatIsNotAPaintingFails()
    {
        _files.WithSource("Broken.reb", "not a painting");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().ContainSingle().Which.Path.Should().Be("Broken.reb");
    }

    private static ColorRgba32[] DecodeTop(DdsImage image, int count = 2)
    {
        BcDecoder decoder = new();
        return decoder.DecodeRaw(image.Data[..16], 4, 4, CompressionFormat.Bc7)[..count];
    }
}
