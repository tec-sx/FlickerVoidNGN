using System.Numerics;

using FlickerVoid.Engine.Rendering.Upscaling;

namespace FlickerVoid.Engine.Rendering.Tests.Upscaling;

public sealed class JitterSequenceTests
{
    private readonly JitterSequenceTestsBuilder _builder = new();

    [Fact]
    public void FollowsTheHaltonSequenceCentredOnThePixel()
    {
        Vector2[] offsets = _builder.Take(3, phases: 8);

        offsets[0].X.Should().BeApproximately(0, 1e-6f, "the sequence starts at its second point, 1/2 across and 1/3 down");
        offsets[0].Y.Should().BeApproximately(-1f / 6, 1e-6f);
        offsets[1].X.Should().BeApproximately(-0.25f, 1e-6f);
        offsets[1].Y.Should().BeApproximately(1f / 6, 1e-6f);
        offsets[2].X.Should().BeApproximately(0.25f, 1e-6f);
        offsets[2].Y.Should().BeApproximately(-7f / 18, 1e-6f);
    }

    [Fact]
    public void StartsOverAfterItsPhases()
    {
        Vector2[] offsets = _builder.Take(19, phases: 18);

        offsets[18].Should().Be(offsets[0]);
        offsets[1..18].Should().NotContain(offsets[0]);
    }

    [Fact]
    public void StaysWithinHalfAPixel()
    {
        Vector2[] offsets = _builder.Take(32, phases: 32);

        offsets.Should().OnlyContain(offset => offset.X >= -0.5f && offset.X < 0.5f && offset.Y >= -0.5f && offset.Y < 0.5f);
    }

    [Theory]
    [InlineData(1920, 1920, 8)]
    [InlineData(1280, 1920, 18)]
    [InlineData(1707, 2560, 18)]
    [InlineData(960, 1920, 32)]
    public void CyclesEightPhasesForEveryOutputPixelARenderPixelCovers(int renderWidth, int outputWidth, int expected)
    {
        int phases = JitterSequence.Phases(renderWidth, outputWidth, denoises: false);

        phases.Should().Be(expected);
    }

    [Theory]
    [InlineData(1920, 1920, 32)]
    [InlineData(1280, 1920, 32)]
    [InlineData(640, 1920, 72)]
    public void CyclesAtLeast32PhasesForAnUpscalerThatDenoises(int renderWidth, int outputWidth, int expected)
    {
        int phases = JitterSequence.Phases(renderWidth, outputWidth, denoises: true);

        phases.Should().Be(expected, "Ray Reconstruction asks for many more phases than plain DLSS, at least 32");
    }
}
