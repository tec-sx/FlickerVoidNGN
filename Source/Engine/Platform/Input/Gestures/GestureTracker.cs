using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Platform.Input.Gestures;

/// <summary>Follows one gesture of one input action at a time: the presses and releases pushed to it and its progress as time passes.</summary>
public sealed class GestureTracker
{
    private Tag _action;
    private Gesture? _gesture;
    private float _elapsed;
    private float _presses;
    private bool _released;
    private bool _cancelled;

    /// <summary>Presses lost per second while mashing.</summary>
    public float MashDecayPerSecond { get; set; }

    /// <summary>How far the gesture is, from 0 to 1.</summary>
    public float Progress { get; private set; }

    public Gesture? Active => _gesture;

    /// <summary>Starts following <paramref name="gesture"/> of <paramref name="action"/>; false while another runs or without an action.</summary>
    public bool Begin(Tag action, Gesture gesture)
    {
        if (_gesture is not null || !action.IsValid)
        {
            return false;
        }

        _action = action;
        _gesture = gesture;
        _elapsed = 0;
        Progress = 0;
        _presses = 0;
        _released = false;
        _cancelled = false;
        return true;
    }

    /// <summary>Counts a press, release or cancel of the followed action; false for any other action or with no gesture.</summary>
    public bool Push(Tag action, InputPhase phase)
    {
        if (_gesture is null || action != _action)
        {
            return false;
        }

        switch (phase)
        {
            case InputPhase.Pressed:
                _presses += 1;
                break;
            case InputPhase.Released:
                _released = true;
                break;
            case InputPhase.Cancelled:
                _cancelled = true;
                break;
            default:
                return false;
        }

        return true;
    }

    /// <summary>Advances the gesture by <paramref name="seconds"/>; zero is the evaluation on the press frame, which adds no time.</summary>
    public GestureStatus Update(float seconds)
    {
        if (_gesture is null)
        {
            return GestureStatus.Inactive;
        }

        if (_cancelled)
        {
            return GestureStatus.Failed;
        }

        float delta = Math.Max(seconds, 0);
        _elapsed += delta;
        switch (_gesture.Mode)
        {
            case GestureMode.Press:
            case GestureMode.Automatic:
                Progress = 1;
                return GestureStatus.Completed;
            case GestureMode.Hold:
                if (_released)
                {
                    return GestureStatus.Failed;
                }

                return EvaluateDuration(_gesture);
            case GestureMode.Hover:
                return EvaluateDuration(_gesture);
            case GestureMode.Mash:
                return EvaluateMash(_gesture, delta);
            default:
                return GestureStatus.Failed;
        }
    }

    public void Reset()
    {
        _gesture = null;
        _action = Tag.None;
        Progress = 0;
    }

    private GestureStatus EvaluateDuration(Gesture gesture)
    {
        Progress = 1;
        if (gesture.Duration > 0)
        {
            Progress = Math.Min(_elapsed / gesture.Duration, 1);
        }

        if (Progress >= 1)
        {
            return GestureStatus.Completed;
        }

        return GestureStatus.Running;
    }

    private GestureStatus EvaluateMash(Gesture gesture, float delta)
    {
        _presses = Math.Max(_presses - (MashDecayPerSecond * delta), 0);
        int pressCount = Math.Max(gesture.PressCount, 1);
        Progress = Math.Min(_presses / pressCount, 1);
        if (Progress >= 1)
        {
            return GestureStatus.Completed;
        }

        bool timedOut = gesture.Duration > 0 && _elapsed >= gesture.Duration;
        if (timedOut)
        {
            return GestureStatus.Failed;
        }

        return GestureStatus.Running;
    }
}
