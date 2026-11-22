using System.Numerics;

using FlickerVoid.Interface.Hud.Minimap;

namespace FlickerVoid.Interface.Tests.Hud.Minimap;

public sealed class MinimapViewModelTests : IDisposable
{
    private readonly MinimapViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void KeepsNorthUpWhileTheCameraLooksNorth()
    {
        MinimapViewModel minimap = _builder.WithCamera(yawDegrees: 0).WithProtagonist(Vector3.Zero, yawDegrees: 0).Build();

        minimap.Update();

        minimap.RingAngle.Should().BeApproximately(0, 0.01);
        minimap.FacingAngle.Should().BeApproximately(180, 0.01, "characters face +Z, which is south");
    }

    [Fact]
    public void TurnsTheRingAndMapAgainstTheCamerasHeading()
    {
        MinimapViewModel minimap = _builder.WithCamera(yawDegrees: -90).WithProtagonist(Vector3.Zero, yawDegrees: 90).Build();

        minimap.Update();

        minimap.RingAngle.Should().BeApproximately(-90, 0.01, "a camera looking east puts east at the top");
        minimap.FacingAngle.Should().BeApproximately(0, 0.01, "the fox faces east too, so it points up");
    }

    [Fact]
    public void ScrollsTheMapUnderTheProtagonistWithinOneTile()
    {
        MinimapViewModel minimap = _builder.WithCamera(yawDegrees: 0).WithProtagonist(new Vector3(2, 0, 3), yawDegrees: 0).Build();

        minimap.Update();

        float tile = MinimapViewModel.TileSize;
        float expectedX = -(2 * MinimapViewModel.PixelsPerMetre) % tile;
        float expectedY = -(3 * MinimapViewModel.PixelsPerMetre) % tile;
        minimap.MapX.Should().BeApproximately(expectedX, 0.01);
        minimap.MapY.Should().BeApproximately(expectedY, 0.01);
    }

    [Fact]
    public void ShowsMarkersAroundTheProtagonistAtTheMapsScale()
    {
        MinimapViewModel minimap = _builder.WithMarker(new Vector3(2, 0, -1)).WithCamera(yawDegrees: 0).WithProtagonist(Vector3.Zero, yawDegrees: 0).Build();

        minimap.Update();

        MinimapMarkerViewModel marker = minimap.Markers.Should().ContainSingle().Subject;
        marker.X.Should().BeApproximately(2 * MinimapViewModel.PixelsPerMetre, 0.01);
        marker.Y.Should().BeApproximately(-1 * MinimapViewModel.PixelsPerMetre, 0.01);
        marker.IsTracked.Should().BeFalse();
    }

    [Fact]
    public void PinsTheTrackedMarkerToTheEdgeOfTheMinimap()
    {
        MinimapViewModel minimap = _builder.WithMarker(new Vector3(0, 0, -40)).WithCamera(yawDegrees: 0).WithProtagonist(Vector3.Zero, yawDegrees: 0)
            .Tracking(0).Build();

        minimap.Update();

        MinimapMarkerViewModel marker = minimap.Markers.Should().ContainSingle().Subject;
        marker.X.Should().BeApproximately(0, 0.01);
        marker.Y.Should().BeApproximately(-MinimapViewModel.Radius, 0.01);
        marker.IsTracked.Should().BeTrue();
    }

    [Fact]
    public void HidesWithoutAProtagonist()
    {
        MinimapViewModel minimap = _builder.WithCamera(yawDegrees: 0).Build();

        minimap.Update();

        minimap.IsVisible.Should().BeFalse();
    }
}
