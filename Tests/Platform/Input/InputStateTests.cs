using System.Numerics;

using FlickerVoid.Engine.Platform.Input;

using Silk.NET.Input;

namespace FlickerVoid.Engine.Platform.Tests.Input;

public class InputStateTests
{
    private readonly InputState _input = new();

    [Fact]
    public void APressLastsOneFrameWhileTheKeyStaysDown()
    {
        _input.PressKey(Key.Space);
        bool pressedFirst = _input.WasPressed(Key.Space);
        _input.EndFrame();
        bool pressedSecond = _input.WasPressed(Key.Space);

        Assert.True(pressedFirst);
        Assert.False(pressedSecond);
        Assert.True(_input.IsDown(Key.Space));
    }

    [Fact]
    public void RepeatsWhileHeldAreNotNewPresses()
    {
        _input.PressKey(Key.W);
        _input.EndFrame();

        _input.PressKey(Key.W);

        Assert.False(_input.WasPressed(Key.W));
    }

    [Fact]
    public void AReleaseLastsOneFrame()
    {
        _input.PressKey(Key.E);
        _input.EndFrame();

        _input.ReleaseKey(Key.E);
        bool releasedFirst = _input.WasReleased(Key.E);
        _input.EndFrame();
        bool releasedSecond = _input.WasReleased(Key.E);

        Assert.True(releasedFirst);
        Assert.False(releasedSecond);
        Assert.False(_input.IsDown(Key.E));
    }

    [Fact]
    public void ATapWithinOneFrameStillCountsAsAPress()
    {
        _input.PressButton(MouseButton.Left);
        _input.ReleaseButton(MouseButton.Left);

        Assert.True(_input.WasPressed(MouseButton.Left));
        Assert.True(_input.WasReleased(MouseButton.Left));
        Assert.False(_input.IsDown(MouseButton.Left));
    }

    [Fact]
    public void MouseMovementAndScrollingAddUpOverAFrame()
    {
        Vector2 start = new(10, 10);
        Vector2 middle = new(15, 12);
        Vector2 end = new(20, 20);
        _input.MoveMouse(start);
        _input.EndFrame();

        _input.MoveMouse(middle);
        _input.MoveMouse(end);
        _input.Scroll(1);
        _input.Scroll(2);

        Vector2 delta = new(10, 10);
        Assert.Equal(end, _input.MousePosition);
        Assert.Equal(delta, _input.MouseDelta);
        Assert.Equal(3, _input.ScrollDelta);
    }

    [Fact]
    public void EndFrameClearsMovementButKeepsThePosition()
    {
        Vector2 position = new(5, 5);
        _input.MoveMouse(position);
        _input.Scroll(1);

        _input.EndFrame();

        Assert.Equal(position, _input.MousePosition);
        Assert.Equal(Vector2.Zero, _input.MouseDelta);
        Assert.Equal(0, _input.ScrollDelta);
    }

    [Fact]
    public void ReleaseAllReleasesEverythingHeld()
    {
        _input.PressKey(Key.ShiftLeft);
        _input.PressButton(MouseButton.Right);
        _input.EndFrame();

        _input.ReleaseAll();

        Assert.False(_input.IsDown(Key.ShiftLeft));
        Assert.False(_input.IsDown(MouseButton.Right));
        Assert.True(_input.WasReleased(Key.ShiftLeft));
        Assert.True(_input.WasReleased(MouseButton.Right));
    }
}
