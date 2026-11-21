using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Input.Gestures;

namespace FlickerVoid.Engine.Platform.Tests.Input.Gestures;

public sealed class GestureTrackerTests
{
    private static readonly Tag Interact = new("Input.Interact");

    private readonly GestureTrackerTestsBuilder _builder = new();

    [Fact]
    public void APressCompletesAtOnce()
    {
        GestureTracker tracker = _builder.Build();
        tracker.Begin(Interact, new Gesture { Mode = GestureMode.Press });

        GestureStatus status = tracker.Update(0);

        status.Should().Be(GestureStatus.Completed);
        tracker.Progress.Should().Be(1);
    }

    [Fact]
    public void AHoldCompletesAfterItsDurationAndFailsWhenReleased()
    {
        GestureTracker held = _builder.Build();
        GestureTracker released = _builder.Build();
        Gesture hold = new() { Mode = GestureMode.Hold, Duration = 1 };
        held.Begin(Interact, hold);
        released.Begin(Interact, hold);

        GestureStatus halfway = held.Update(0.5f);
        float progress = held.Progress;
        GestureStatus done = held.Update(0.5f);
        released.Push(Interact, InputPhase.Released);
        GestureStatus failed = released.Update(0.1f);

        halfway.Should().Be(GestureStatus.Running);
        progress.Should().Be(0.5f);
        done.Should().Be(GestureStatus.Completed);
        failed.Should().Be(GestureStatus.Failed);
    }

    [Fact]
    public void AMashCompletesAfterItsPressesOrFailsWhenTimeRunsOut()
    {
        GestureTracker tracker = _builder.Build();
        tracker.Begin(Interact, new Gesture { Mode = GestureMode.Mash, PressCount = 2, Duration = 1 });
        tracker.Push(Interact, InputPhase.Pressed);
        GestureStatus first = tracker.Update(0.2f);
        tracker.Push(Interact, InputPhase.Pressed);

        GestureStatus second = tracker.Update(0.2f);

        first.Should().Be(GestureStatus.Running);
        second.Should().Be(GestureStatus.Completed);
        GestureTracker slow = _builder.Build();
        slow.Begin(Interact, new Gesture { Mode = GestureMode.Mash, PressCount = 2, Duration = 1 });
        slow.Update(1).Should().Be(GestureStatus.Failed);
    }

    [Fact]
    public void MashPressesDecayOverTime()
    {
        GestureTracker tracker = _builder.WithMashDecay(1).Build();
        tracker.Begin(Interact, new Gesture { Mode = GestureMode.Mash, PressCount = 2 });
        tracker.Push(Interact, InputPhase.Pressed);

        tracker.Update(0.5f);

        tracker.Progress.Should().Be(0.25f);
    }

    [Fact]
    public void IgnoresOtherActionsAndASecondGesture()
    {
        GestureTracker tracker = _builder.Build();
        tracker.Begin(Interact, new Gesture());

        bool pushed = tracker.Push(new Tag("Input.Jump"), InputPhase.Pressed);
        bool begun = tracker.Begin(Interact, new Gesture());

        pushed.Should().BeFalse();
        begun.Should().BeFalse();
    }

    [Fact]
    public void ACancelFails()
    {
        GestureTracker tracker = _builder.Build();
        tracker.Begin(Interact, new Gesture { Mode = GestureMode.Hover, Duration = 2 });
        tracker.Push(Interact, InputPhase.Cancelled);

        tracker.Update(0.1f).Should().Be(GestureStatus.Failed);
        tracker.Reset();
        tracker.Update(0.1f).Should().Be(GestureStatus.Inactive);
    }
}
