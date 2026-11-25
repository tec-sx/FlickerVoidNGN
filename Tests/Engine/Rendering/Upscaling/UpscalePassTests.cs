using System.Numerics;

using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.Upscaling;

namespace FlickerVoid.Engine.Rendering.Tests.Upscaling;

public sealed class UpscalePassTests : IDisposable
{
    private const int Size = UpscalePassTestsBuilder.OutputSize;

    /// <summary>Two thirds of the output, which the stand-in draws Quality frames at.</summary>
    private const int RenderSize = Size * 2 / 3;

    private const int Centre = (Size / 2 * Size) + (Size / 2);

    /// <summary>An orthographic camera seeing two units across, so a unit box covers the middle half of the view.</summary>
    private const string Camera = """
        { "name": "Camera", "components": { "transform": { "position": [0, 0, 5] }, "camera": { "orthographicHeight": 2 } } }
        """;

    private const string Box = """{ "name": "Box", "components": { "transform": { "position": [0, -0.5, 0] }, "greybox": { "size": [1, 1, 1] } } }""";

    /// <summary>The box, outlined, and moved right by 0.6 output pixels, so its sides lie close to pixels' centres, where jitter would move them across.</summary>
    private const string OutlinedBox = """
        { "name": "Box", "components": { "transform": { "position": [0.025, -0.5, 0] }, "greybox": { "size": [1, 1, 1] }, "outline": { "color": [0, 1, 0] } } }
        """;

    private readonly UpscalePassTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void DrawsAtTheUpscalersSizeAndFillsTheOutputFromIt()
    {
        using UpscalePassTestsBuilder plain = new();
        plain.WithScene(Camera, Box);
        _builder.WithScene(Camera, Box).WithDlss().WithUpscaling(UpscaleMode.Quality);

        uint[] expected = plain.Render();
        uint[] pixels = _builder.Render();

        StandInUpscaler upscaler = _builder.Upscalers.Should().ContainSingle().Subject;
        upscaler.Description.Should().Be(new UpscalerDescription(UpscaleMode.Quality, Size, Size));
        UpscaleInputs frame = upscaler.Frames.Should().ContainSingle().Subject;
        frame.Color.Description.Width.Should().Be(RenderSize);
        frame.Color.Description.Height.Should().Be(RenderSize);
        frame.Depth.Description.Width.Should().Be(RenderSize);
        frame.Motion.Description.Width.Should().Be(RenderSize);
        frame.Output.Description.Width.Should().Be(Size);
        frame.Output.Description.Height.Should().Be(Size);
        pixels[Centre].Should().Be(expected[Centre], "the box's middle looks as without upscaling");
        pixels[Size + 1].Should().Be(expected[Size + 1], "and so does the empty corner");
    }

    [Fact]
    public void DrawsAsBeforeWhereTheDeviceHasNoDlss()
    {
        using UpscalePassTestsBuilder plain = new();
        plain.WithScene(Camera, Box);
        _builder.WithScene(Camera, Box).WithUpscaling(UpscaleMode.Quality);

        uint[] expected = plain.Render();
        uint[] pixels = _builder.Render();

        pixels.Should().Equal(expected);
    }

    [Fact]
    public void UpscalesNothingWhileSwitchedOff()
    {
        _builder.WithScene(Camera, Box).WithDlss().WithUpscaling(UpscaleMode.Quality);
        _builder.Render();

        _builder.WithUpscaling(UpscaleMode.Off);
        _builder.Render();

        StandInUpscaler upscaler = _builder.Upscalers.Should().ContainSingle().Subject;
        upscaler.Frames.Should().ContainSingle("the second frame was drawn without it");
        upscaler.Disposed.Should().BeTrue();
    }

    [Fact]
    public void JittersEveryFrameThroughTheHaltonSequence()
    {
        _builder.WithScene(Camera, Box).WithDlss().WithUpscaling(UpscaleMode.Quality);

        _builder.RenderFrames(3);

        JitterSequence sequence = new();
        int phases = JitterSequence.Phases(RenderSize, Size);
        Vector2[] expected = [sequence.Next(phases), sequence.Next(phases), sequence.Next(phases)];
        StandInUpscaler upscaler = _builder.Upscalers.Should().ContainSingle().Subject;
        upscaler.Frames.Select(static frame => frame.Jitter).Should().Equal(expected);
    }

    [Fact]
    public void LeavesTheJitterOutOfTheMotion()
    {
        _builder.WithScene(Camera, Box).WithDlss().WithUpscaling(UpscaleMode.Quality);

        Vector2[] motion = _builder.RenderMotion(frames: 3);

        motion.Should().HaveCount(RenderSize * RenderSize);
        motion.Should().OnlyContain(static pixel => pixel.Length() < 0.01f, "a still box seen by a still camera has not moved, however each frame was jittered");
    }

    [Fact]
    public void StartsTheUpscalersHistoryAfreshOnTheFirstFrameAndAfterACut()
    {
        _builder.WithScene(Camera, Box).WithDlss().WithUpscaling(UpscaleMode.Quality);
        _builder.RenderFrames(2);

        _builder.WithCut();
        _builder.RenderFrames(2);

        StandInUpscaler upscaler = _builder.Upscalers.Should().ContainSingle().Subject;
        upscaler.Frames.Select(static frame => frame.Reset).Should().Equal(true, false, true, false);
    }

    [Fact]
    public void MakesANewUpscalerWhenTheOutputChangesSize()
    {
        _builder.WithScene(Camera, Box).WithDlss().WithUpscaling(UpscaleMode.Quality);
        _builder.Render(Size);

        _builder.Render(60);

        _builder.Upscalers.Should().HaveCount(2);
        _builder.Upscalers[0].Disposed.Should().BeTrue();
        _builder.Upscalers[1].Description.Should().Be(new UpscalerDescription(UpscaleMode.Quality, 60, 60));
        _builder.Upscalers[1].Frames.Should().ContainSingle().Which.Reset.Should().BeTrue();
    }

    [Fact]
    public void SmoothsAFrameDrawnAtTheOutputsSizeForDlaa()
    {
        _builder.WithScene(Camera, Box).WithDlss().WithUpscaling(UpscaleMode.Dlaa);

        _builder.Render();

        UpscaleInputs frame = _builder.Upscalers.Should().ContainSingle().Subject.Frames.Should().ContainSingle().Subject;
        frame.Color.Description.Width.Should().Be(Size);
        frame.Output.Should().NotBeSameAs(frame.Color, "DLSS writes a target of its own");
        frame.Jitter.Should().NotBe(Vector2.Zero);
    }

    [Fact]
    public void GivesTheUpscalerAnOutputItCanWriteAndClear()
    {
        _builder.WithScene(Camera, Box).WithDlss().WithUpscaling(UpscaleMode.Quality);

        _builder.Render();

        UpscaleInputs frame = _builder.Upscalers.Should().ContainSingle().Subject.Frames.Should().ContainSingle().Subject;
        frame.Output.Description.Usage.Should().HaveFlag(TextureUsage.ShaderWrite);
        frame.Output.Description.Usage.Should().HaveFlag(TextureUsage.CopyDestination, "DLSS clears its output, which Vulkan allows only on images made to be copied to");
    }

    [Fact]
    public void LaysThePaperOverTheUpscaledFrameWhereNothingWasPainted()
    {
        using UpscalePassTestsBuilder plain = new();
        plain.WithScene(Camera, Box).WithPaper(200);
        _builder.WithScene(Camera, Box).WithPaper(200).WithDlss().WithUpscaling(UpscaleMode.Quality);

        uint[] expected = plain.Render();
        uint[] pixels = _builder.Render();

        foreach (int pixel in (int[])[Size + 1, Centre])
        {
            (byte red, byte green, byte blue) = UpscalePassTestsBuilder.Channels(expected[pixel]);
            (byte upscaledRed, byte upscaledGreen, byte upscaledBlue) = UpscalePassTestsBuilder.Channels(pixels[pixel]);
            upscaledRed.Should().BeCloseTo(red, 2, "the corner shows the bare paper and the box's middle its paint, as without upscaling");
            upscaledGreen.Should().BeCloseTo(green, 2);
            upscaledBlue.Should().BeCloseTo(blue, 2);
        }
    }

    [Fact]
    public void KeepsOutlinesStillAsTheFramesJitter()
    {
        _builder.WithScene(Camera, OutlinedBox).WithDlss().WithUpscaling(UpscaleMode.Quality);

        uint[] first = _builder.Render();
        uint[] second = _builder.Render();

        uint green = Rgba(0, 255, 0, 255);
        int[] firstLine = [.. Enumerable.Range(0, first.Length).Where(pixel => first[pixel] == green)];
        int[] secondLine = [.. Enumerable.Range(0, second.Length).Where(pixel => second[pixel] == green)];
        firstLine.Should().NotBeEmpty();
        secondLine.Should().Equal(firstLine, "outlines are drawn at the output's size, without the jitter");
    }

    [Fact]
    public void ShowsTheGBufferOverTheWholeOutputInADebugView()
    {
        _builder.WithScene(Camera, Box).WithDlss().WithUpscaling(UpscaleMode.Quality).WithRenderer(static renderer => renderer.DebugView = DebugView.Albedo);

        uint[] pixels = _builder.Render();

        int nearBottomRight = ((Size * 3 / 4) - 2) * (Size + 1);
        pixels[nearBottomRight].Should().Be(pixels[Centre], "the box's albedo shows up to its lower right corner");
        UpscalePassTestsBuilder.Channels(pixels[Centre]).Red.Should().BeGreaterThan(0);
    }

    private static uint Rgba(byte r, byte g, byte b, byte a) => (uint)(r | g << 8 | b << 16 | a << 24);
}
