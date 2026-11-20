using System.Numerics;

using Avalonia.Input;

using FlickerVoid.Engine.Editor.Cameras;

namespace FlickerVoid.Engine.Editor.Tests.Cameras;

public sealed class EditorCameraControllerTests
{
    private const float Precision = EditorCameraTestsBuilder.Precision;

    private readonly EditorCameraControllerTestsBuilder _builder = new();

    [Fact]
    public void OrbitsWithAltAndTheLeftButton()
    {
        Vector3 pivot = _builder.Camera.Pivot;
        float yaw = _builder.Camera.Yaw;

        _builder.HoldingAlt().Holding(ViewportButtons.Left).Dragging(40, 0).Apply();

        Vector3.Distance(_builder.Camera.Pivot, pivot).Should().BeLessThan(Precision);
        _builder.Camera.Yaw.Should().BeLessThan(yaw, "dragging right swings the camera round to the left, turning it right");
    }

    [Fact]
    public void PansWithAltAndTheMiddleButton()
    {
        Vector3 position = _builder.Camera.Position;
        float yaw = _builder.Camera.Yaw;

        _builder.HoldingAlt().Holding(ViewportButtons.Middle).Dragging(40, 20).Apply();

        _builder.Camera.Yaw.Should().Be(yaw);
        Vector3.Distance(_builder.Camera.Position, position).Should().BeGreaterThan(0.1f);
    }

    [Fact]
    public void ZoomsWithAltAndTheRightButton()
    {
        Vector3 pivot = _builder.Camera.Pivot;

        _builder.HoldingAlt().Holding(ViewportButtons.Right).Dragging(30, -30).Apply();

        _builder.Camera.PivotDistance.Should().BeLessThan(10, "dragging up or right zooms in");
        Vector3.Distance(_builder.Camera.Pivot, pivot).Should().BeLessThan(Precision);
    }

    [Fact]
    public void ZoomsWithTheWheel()
    {
        _builder.Scrolling(2).Apply();

        _builder.Camera.PivotDistance.Should().BeLessThan(10);
    }

    [Fact]
    public void LooksAroundWithTheRightButton()
    {
        Vector3 position = _builder.Camera.Position;
        float yaw = _builder.Camera.Yaw;

        _builder.Holding(ViewportButtons.Right).Dragging(40, 0).Apply();

        Vector3.Distance(_builder.Camera.Position, position).Should().BeLessThan(Precision);
        _builder.Camera.Yaw.Should().BeLessThan(yaw);
    }

    [Theory]
    [InlineData(Key.W)]
    [InlineData(Key.A)]
    [InlineData(Key.S)]
    [InlineData(Key.D)]
    [InlineData(Key.Q)]
    [InlineData(Key.E)]
    public void FliesWithTheMovementKeysWhileTheRightButtonIsHeld(Key key)
    {
        Vector3 position = _builder.Camera.Position;

        _builder.Holding(ViewportButtons.Right).HoldingKey(key).Apply();

        Vector3.Distance(_builder.Camera.Position, position).Should().BeApproximately(_builder.Camera.FlySpeed * 0.1f, Precision);
    }

    [Fact]
    public void FliesForwardWithW()
    {
        Vector3 position = _builder.Camera.Position;
        Vector3 forward = _builder.Camera.Forward;

        _builder.Holding(ViewportButtons.Right).HoldingKey(Key.W).Apply();

        Vector3 moved = _builder.Camera.Position - position;
        Vector3.Dot(moved, forward).Should().BeGreaterThan(0);
    }

    [Fact]
    public void LeavesTheMovementKeysToTheGizmosWithoutTheRightButton()
    {
        Vector3 position = _builder.Camera.Position;

        _builder.HoldingKey(Key.W).HoldingKey(Key.E).Apply();

        _builder.Camera.Position.Should().Be(position);
    }

    [Fact]
    public void ChangesFlyingSpeedWithTheWheelWhileTheRightButtonIsHeld()
    {
        float speed = _builder.Camera.FlySpeed;

        _builder.Holding(ViewportButtons.Right).Scrolling(1).Apply();

        _builder.Camera.FlySpeed.Should().BeGreaterThan(speed);
        _builder.Camera.PivotDistance.Should().Be(10);
    }
}
