using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;
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
    public void ShowsAnOrthographicViewTheSunThroughItsVirtualLens()
    {
        using SkyPassTestsBuilder turnedAway = new();
        Vector3 ahead = Vector3.Normalize(new Vector3(0, -0.15f, 1));
        _builder.LookingDownOrthographically().WithMood(mood => mood with { SunDirection = ahead });
        turnedAway.LookingDownOrthographically().TurnedAround().WithMood(mood => mood with { SunDirection = ahead });

        int facing = Brightest(_builder.Render());
        int away = Brightest(turnedAway.Render());

        facing.Should().BeGreaterThan(away + 150, "the sun ahead of the camera shows, and behind it does not");
    }

    [Fact]
    public void KeepsTheHorizonMidwayUpAnOrthographicViewHoweverItTilts()
    {
        _builder.LookingDownOrthographically().WithMood(static mood => mood with { HorizonSoftness = 0 });

        uint[] pixels = _builder.Render();

        int step = Step(pixels);
        step.Should().BeGreaterThan(60, "the virtual lens looks level, so sky meets ground in the middle");
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
    public void PaintsTheZenithHorizonAndGroundColorsTheMoodGives()
    {
        _builder.WithMood(static mood => mood with { SkyZenith = new Vector3(0.6f, 0.05f, 0.05f), SkyHorizon = new Vector3(0.3f, 0.3f, 0.3f), SkyGround = new Vector3(0.02f, 0.3f, 0.02f) });

        uint[] pixels = _builder.Render();

        (byte topRed, byte topGreen, _) = SkyPassTestsBuilder.Channels(pixels[(TopRow * Size) + Centre]);
        (byte bottomRed, byte bottomGreen, _) = SkyPassTestsBuilder.Channels(pixels[(BottomRow * Size) + Centre]);
        topRed.Should().BeGreaterThan((byte)(topGreen + 40), "the sky turns to the zenith's red overhead");
        bottomGreen.Should().BeGreaterThan((byte)(bottomRed + 20), "the ground is the mood's green");
    }

    [Fact]
    public void BlendsSkyAndGroundOverTheHorizonsSoftness()
    {
        using SkyPassTestsBuilder soft = new();
        soft.WithMood(static mood => mood with { HorizonSoftness = 0.3f });

        uint[] sharpPixels = _builder.Render();
        uint[] softPixels = soft.Render();

        int sharpStep = Step(sharpPixels);
        int softStep = Step(softPixels);
        softStep.Should().BeLessThan(sharpStep / 2, "a soft horizon has no hard line between sky and ground");
    }

    [Fact]
    public void DrawsTheSunsDiscAsLightWhereTheSunIsInView()
    {
        using SkyPassTestsBuilder hidden = new();
        Vector3 inView = Vector3.Normalize(new Vector3(0, -0.15f, 1));
        _builder.WithMood(mood => mood with { SunDirection = inView });
        hidden.WithMood(mood => mood with { SunDirection = inView, SunDiscIntensity = 0, SunGlow = 0 });

        int withSun = Brightest(_builder.Render());
        int withoutSun = Brightest(hidden.Render());

        withSun.Should().BeGreaterThan(withoutSun + 150);
    }

    [Fact]
    public void LightsTheMoonsFaceByWhereTheSunIs()
    {
        using SkyPassTestsBuilder newMoon = new();
        Vector3 moon = Vector3.Normalize(new Vector3(0, -0.15f, 1));
        _builder.WithMood(mood => mood with { SunDirection = Vector3.Normalize(new Vector3(0, 0.4f, -1)), MoonDirection = moon, SunDiscIntensity = 0, SunGlow = 0 });
        newMoon.WithMood(mood => mood with { SunDirection = Vector3.Normalize(new Vector3(0, 0.05f, 1)), MoonDirection = moon, SunDiscIntensity = 0, SunGlow = 0 });

        int full = Brightest(_builder.Render());
        int dark = Brightest(newMoon.Render());

        full.Should().BeGreaterThan(dark + 100, "a full moon is lit whole and a new one turns its dark side to us");
    }

    [Fact]
    public void ShowsStarsInADarkSky()
    {
        using SkyPassTestsBuilder starless = new();
        _builder.WithMood(static mood => NightSky(mood) with { StarIntensity = 4 });
        starless.WithMood(static mood => NightSky(mood) with { StarIntensity = 0 });

        int withStars = Brightest(_builder.Render());
        int withoutStars = Brightest(starless.Render());

        withStars.Should().BeGreaterThan(withoutStars + 60);
    }

    [Fact]
    public void HidesTheStarsUnderABrightSky()
    {
        using SkyPassTestsBuilder starless = new();
        _builder.WithMood(static mood => mood with { StarIntensity = 4, SunGlow = 0, SunDiscIntensity = 0, MoonDiscIntensity = 0, MoonCorona = 0 });
        starless.WithMood(static mood => mood with { StarIntensity = 0, SunGlow = 0, SunDiscIntensity = 0, MoonDiscIntensity = 0, MoonCorona = 0 });

        uint[] stars = _builder.Render();
        uint[] none = starless.Render();

        stars.Should().Equal(none, "the day's sky drowns them");
    }

    [Fact]
    public void TurnsTheStarsWithTheSun()
    {
        using SkyPassTestsBuilder later = new();
        _builder.WithMood(static mood => NightSky(mood) with { StarIntensity = 4, SunDirection = Vector3.Normalize(new Vector3(0.3f, 1, 0)) });
        later.WithMood(static mood => NightSky(mood) with { StarIntensity = 4, SunDirection = Vector3.Normalize(new Vector3(-0.3f, 1, 0)) });

        uint[] early = _builder.Render();
        uint[] turned = later.Render();

        early.Should().NotEqual(turned);
    }

    [Fact]
    public void DrawsNothingWhileTheRendererHasNoSky()
    {
        _builder.Render();
        _builder.Renderer!.Sky = false;

        uint[] pixels = _builder.Render();

        SkyPassTestsBuilder.Channels(pixels[Centre]).Should().Be(((byte)0, (byte)0, (byte)0));
    }

    /// <summary>A dark sky without the sun's and moon's light, the sun below the horizon.</summary>
    private static LightingMood NightSky(LightingMood mood)
    {
        Vector3 dark = new(0.002f, 0.002f, 0.004f);
        return mood with
        {
            SkyZenith = dark,
            SkyHorizon = dark,
            SkyGround = dark,
            SunDirection = Vector3.UnitY,
            SunGlow = 0,
            SunDiscIntensity = 0,
            MoonDiscIntensity = 0,
            MoonCorona = 0,
        };
    }

    private static int Brightest(uint[] pixels)
    {
        int brightest = 0;
        foreach (uint pixel in pixels)
        {
            brightest = Math.Max(brightest, SkyPassTestsBuilder.Brightness(pixel));
        }

        return brightest;
    }

    /// <summary>The change in brightness across the horizon, between the rows just above and below the middle.</summary>
    private static int Step(uint[] pixels)
    {
        int above = SkyPassTestsBuilder.Brightness(pixels[((Centre - 2) * Size) + Centre]);
        int below = SkyPassTestsBuilder.Brightness(pixels[((Centre + 2) * Size) + Centre]);
        return Math.Abs(above - below);
    }
}
