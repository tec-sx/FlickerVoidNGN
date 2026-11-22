using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Engine.Cameras.Tests.Transitions;

public sealed class TransitionPassTests : IDisposable
{
    /// <summary>Two of the corner pixels, outside the quad, and two of its middle ones.</summary>
    private const int Corner = 0;
    private const int Middle = 5;

    private readonly TransitionPassTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void DrawsTheWorldUnchangedWithoutATransition()
    {
        ActiveCamera active = new() { Lens = TransitionPassTestsBuilder.Lens, Pose = TransitionPassTestsBuilder.OnTheQuad };

        uint[] pixels = _builder.Render(active);

        TransitionPassTestsBuilder.Channels(pixels[Middle]).Should().Be((255, 0, 0, 255));
        TransitionPassTestsBuilder.Channels(pixels[Corner]).Should().Be((0, 0, 0, 0));
    }

    [Fact]
    public void FadesTheWorldTowardsTheFadeColour()
    {
        ActiveCamera active = new()
        {
            Lens = TransitionPassTestsBuilder.Lens,
            Pose = TransitionPassTestsBuilder.OnTheQuad,
            Fade = 0.5f,
            FadeColor = new Vector3(0, 0, 1),
        };

        uint[] pixels = _builder.Render(active);

        (byte red, byte green, byte blue, _) = TransitionPassTestsBuilder.Channels(pixels[Middle]);
        red.Should().BeInRange(126, 129);
        green.Should().Be(0);
        blue.Should().BeInRange(126, 129);
        (byte cornerRed, _, byte cornerBlue, _) = TransitionPassTestsBuilder.Channels(pixels[Corner]);
        cornerRed.Should().Be(0);
        cornerBlue.Should().BeInRange(126, 129);
    }

    [Fact]
    public void AFullFadeHidesTheWorld()
    {
        ActiveCamera active = new()
        {
            Lens = TransitionPassTestsBuilder.Lens,
            Pose = TransitionPassTestsBuilder.OnTheQuad,
            Fade = 1,
            FadeColor = Vector3.Zero,
        };

        uint[] pixels = _builder.Render(active);

        pixels.Should().AllSatisfy(pixel => TransitionPassTestsBuilder.Channels(pixel).Should().Be((0, 0, 0, 255)));
    }

    [Fact]
    public void DissolvesThePreviousShotsLiveViewAway()
    {
        ActiveCamera active = new()
        {
            Lens = TransitionPassTestsBuilder.Lens,
            Pose = TransitionPassTestsBuilder.AwayFromTheQuad,
            Dissolve = 0.5f,
            DissolveLens = TransitionPassTestsBuilder.Lens,
            DissolvePose = TransitionPassTestsBuilder.OnTheQuad,
        };

        uint[] pixels = _builder.Render(active);

        (byte red, byte green, byte blue, _) = TransitionPassTestsBuilder.Channels(pixels[Middle]);
        red.Should().BeInRange(126, 129, "half of the old view, where it saw the quad, shows over the new one");
        green.Should().Be(0);
        blue.Should().Be(0);
        TransitionPassTestsBuilder.Channels(pixels[Corner]).Red.Should().Be(0);
    }
}
