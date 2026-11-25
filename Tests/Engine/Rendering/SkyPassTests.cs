using System.Numerics;

using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class SkyPassTests : IDisposable
{
    private const int Size = SkyPassTestsBuilder.TargetSize;
    private const int Centre = Size / 2;
    private const int TopRow = 1;
    private const int BottomRow = Size - 2;

    private readonly SkyPassTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void FillsTheEmptyViewLighterAboveTheHorizonThanBelow()
    {
        uint[] pixels = _builder.Render();

        int above = SkyPassTestsBuilder.Brightness(pixels[(TopRow * Size) + Centre]);
        int below = SkyPassTestsBuilder.Brightness(pixels[(BottomRow * Size) + Centre]);
        below.Should().BeGreaterThan(0);
        above.Should().BeGreaterThan(below);
    }

    [Fact]
    public void ShadesAnOrthographicViewFromTopToBottomToo()
    {
        _builder.LookingDownOrthographically();

        uint[] pixels = _builder.Render();

        int top = SkyPassTestsBuilder.Brightness(pixels[(TopRow * Size) + Centre]);
        int bottom = SkyPassTestsBuilder.Brightness(pixels[(BottomRow * Size) + Centre]);
        top.Should().BeGreaterThan(bottom);
    }

    [Fact]
    public void LetsThePaperShowThroughTheSkyLikeAThinWash()
    {
        using SkyPassTestsBuilder dark = new();
        _builder.WithPaper(220);
        dark.WithPaper(110);

        uint[] onLight = _builder.Render();
        uint[] onDark = dark.Render();

        int light = SkyPassTestsBuilder.Brightness(onLight[(TopRow * Size) + Centre]);
        int darker = SkyPassTestsBuilder.Brightness(onDark[(TopRow * Size) + Centre]);
        light.Should().BeGreaterThan(darker + 60, "the sky is laid over the paper, not over it like a wall");
    }

    [Theory]
    [InlineData(Tonemapper.Uncharted2)]
    [InlineData(Tonemapper.Aces)]
    public void KeepsItsColoursOnScreenWhateverTheMoodsGrade(Tonemapper tonemapper)
    {
        using SkyPassTestsBuilder graded = new();
        graded.WithMood(mood => mood with { Exposure = 2, Contrast = 1.2f, Saturation = 0.8f, Tint = new Vector3(1, 0.9f, 0.8f), Tonemapper = tonemapper });

        uint[] plain = _builder.Render();
        uint[] gradedPixels = graded.Render();

        foreach (int row in (int[])[TopRow, Centre, BottomRow])
        {
            (byte red, byte green, byte blue) = SkyPassTestsBuilder.Channels(plain[(row * Size) + Centre]);
            (byte gradedRed, byte gradedGreen, byte gradedBlue) = SkyPassTestsBuilder.Channels(gradedPixels[(row * Size) + Centre]);
            gradedRed.Should().BeCloseTo(red, 2, "the sky's colours are picked for the screen, and the grade gives them back");
            gradedGreen.Should().BeCloseTo(green, 2);
            gradedBlue.Should().BeCloseTo(blue, 2);
        }
    }

    [Fact]
    public void DrawsNothingWhileTheRendererHasNoSky()
    {
        _builder.Render();
        _builder.Renderer!.Sky = false;

        uint[] pixels = _builder.Render();

        SkyPassTestsBuilder.Channels(pixels[Centre]).Should().Be(((byte)0, (byte)0, (byte)0));
    }
}
