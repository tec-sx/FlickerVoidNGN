namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class ProbePassTests : IDisposable
{
    private readonly ProbePassTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void BouncesALitWallsColourOntoTheFloor()
    {
        uint[] pixels = _builder.WithRooms(doorOpen: true).Traced().Render(frames: 8);

        (byte red, byte green, _) = SceneRendererTestsBuilder.Channels(pixels[ProbePassTestsBuilder.FloorBesideTheRedWall]);
        red.Should().BeGreaterThan(10, "the sunlit red wall beside it lights the floor, which no light reaches straight");
        green.Should().Be(0, "nothing green lights the rooms");
    }

    [Fact]
    public void LeavesTheFloorDarkWithoutProbes()
    {
        uint[] pixels = _builder.WithRooms(doorOpen: true).WithoutProbes().Render(frames: 2);

        (byte red, _, _) = SceneRendererTestsBuilder.Channels(pixels[ProbePassTestsBuilder.FloorBesideTheRedWall]);
        red.Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StopsTheBounceAtAClosedDoor(bool doorOpen)
    {
        uint[] pixels = _builder.WithRooms(doorOpen).Traced().Render(frames: 8);

        (byte red, _, _) = SceneRendererTestsBuilder.Channels(pixels[ProbePassTestsBuilder.FloorBehindTheDoor]);
        if (doorOpen)
        {
            red.Should().BeGreaterThan(3, "the red wall shows through the doorway");
        }
        else
        {
            red.Should().Be(0, "the probes beyond the door, which can't see the floor behind it, don't light it");
        }
    }

    [Fact]
    public void LightsAWallWithAGlowingPanel()
    {
        uint[] pixels = _builder.WithGlowingPanel().Traced().Render(frames: 8);

        (byte red, byte green, _) = SceneRendererTestsBuilder.Channels(pixels[ProbePassTestsBuilder.WallAboveThePanel]);
        green.Should().BeGreaterThan(20, "the panel's glow lights the wall before it");
        red.Should().Be(0);
    }

    [Fact]
    public void BakesThroughRaysWhatTheLiveProbesSettleOn()
    {
        _builder.WithRooms(doorOpen: true).Baked();
        uint[] baked = _builder.Untraced().Render(frames: 1);
        uint[] live = _builder.Traced().Render(frames: 64);

        int[] watched = [ProbePassTestsBuilder.FloorBesideTheRedWall, ProbePassTestsBuilder.FloorBehindTheDoor];
        foreach (int pixel in watched)
        {
            (byte bakedRed, _, _) = SceneRendererTestsBuilder.Channels(baked[pixel]);
            (byte liveRed, _, _) = SceneRendererTestsBuilder.Channels(live[pixel]);
            int tolerance = Math.Max(3, bakedRed / 6);
            bakedRed.Should().BeGreaterThan(3);
            ((int)liveRed).Should().BeCloseTo(bakedRed, (uint)tolerance, "the bake traces the same rays the live probes do");
        }
    }
}
