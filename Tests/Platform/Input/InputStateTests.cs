using System.Numerics;

using FlickerVoid.Engine.Platform.Input;

using Silk.NET.Input;

namespace FlickerVoid.Engine.Platform.Tests.Input;

public class InputStateTests
{
    private readonly InputStateTestsBuilder _builder = new();

    [Fact]
    public void APressLastsOneFrameWhileTheKeyStaysDown()
    {
        InputState input = _builder.Build();

        input.PressKey(Key.Space);
        bool pressedFirst = input.WasPressed(Key.Space);
        input.EndFrame();
        bool pressedSecond = input.WasPressed(Key.Space);

        bool down = input.IsDown(Key.Space);
        pressedFirst.Should().BeTrue();
        pressedSecond.Should().BeFalse();
        down.Should().BeTrue();
    }

    [Fact]
    public void RepeatsWhileHeldAreNotNewPresses()
    {
        InputState input = _builder.WithKeyHeld(Key.W).Build();

        input.PressKey(Key.W);

        bool pressed = input.WasPressed(Key.W);
        pressed.Should().BeFalse();
    }

    [Fact]
    public void AReleaseLastsOneFrame()
    {
        InputState input = _builder.WithKeyHeld(Key.E).Build();

        input.ReleaseKey(Key.E);
        bool releasedFirst = input.WasReleased(Key.E);
        input.EndFrame();
        bool releasedSecond = input.WasReleased(Key.E);

        bool down = input.IsDown(Key.E);
        releasedFirst.Should().BeTrue();
        releasedSecond.Should().BeFalse();
        down.Should().BeFalse();
    }

    [Fact]
    public void ATapWithinOneFrameStillCountsAsAPress()
    {
        InputState input = _builder.Build();

        input.PressButton(MouseButton.Left);
        input.ReleaseButton(MouseButton.Left);

        bool pressed = input.WasPressed(MouseButton.Left);
        bool released = input.WasReleased(MouseButton.Left);
        bool down = input.IsDown(MouseButton.Left);
        pressed.Should().BeTrue();
        released.Should().BeTrue();
        down.Should().BeFalse();
    }

    [Fact]
    public void MouseMovementAndScrollingAddUpOverAFrame()
    {
        Vector2 start = new(10, 10);
        Vector2 middle = new(15, 12);
        Vector2 end = new(20, 20);
        InputState input = _builder.WithMouseAt(start).Build();

        input.MoveMouse(middle);
        input.MoveMouse(end);
        input.Scroll(1);
        input.Scroll(2);

        Vector2 delta = new(10, 10);
        input.MousePosition.Should().Be(end);
        input.MouseDelta.Should().Be(delta);
        input.ScrollDelta.Should().Be(3);
    }

    [Fact]
    public void EndFrameClearsMovementButKeepsThePosition()
    {
        Vector2 position = new(5, 5);
        InputState input = _builder.Build();
        input.MoveMouse(position);
        input.Scroll(1);

        input.EndFrame();

        input.MousePosition.Should().Be(position);
        input.MouseDelta.Should().Be(Vector2.Zero);
        input.ScrollDelta.Should().Be(0);
    }

    [Fact]
    public void ReleaseAllReleasesEverythingHeld()
    {
        InputState input = _builder.WithKeyHeld(Key.ShiftLeft).WithButtonHeld(MouseButton.Right).Build();

        input.ReleaseAll();

        bool keyDown = input.IsDown(Key.ShiftLeft);
        bool buttonDown = input.IsDown(MouseButton.Right);
        bool keyReleased = input.WasReleased(Key.ShiftLeft);
        bool buttonReleased = input.WasReleased(MouseButton.Right);
        keyDown.Should().BeFalse();
        buttonDown.Should().BeFalse();
        keyReleased.Should().BeTrue();
        buttonReleased.Should().BeTrue();
    }
}
