using System.Diagnostics;

using Avalonia.Threading;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// The one clock every pulsing thing follows, so whatever waits for its beat pulses together: on the beat of the playing
/// arrangement when an editor gives its beat, else at a resting tempo. It ticks about 30 times a second while anything listens.
/// </summary>
public static class BeatPulse
{
    /// <summary>The beats a second when nothing plays, as 96 BPM.</summary>
    public const double RestingBeatsPerSecond = 1.6;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly HashSet<Action> Listeners = [];
    private static DispatcherTimer? _timer;
    private static Func<double>? _beat;

    /// <summary>From 0 to 1: 1 on the beat, easing down to 0 before the next.</summary>
    public static double Value
    {
        get
        {
            double beat = CurrentBeat();
            double phase = beat - Math.Floor(beat);
            double left = 1 - phase;
            return left * left;
        }
    }

    /// <summary>Follows the beats of what plays (counted from its start), or the resting tempo again with null.</summary>
    public static void Follow(Func<double>? beat) => _beat = beat;

    /// <summary>Calls the action on every tick until <see cref="Ignore"/>; the clock runs only while something listens.</summary>
    public static void Listen(Action tick)
    {
        Listeners.Add(tick);
        if (_timer is not null)
        {
            return;
        }

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, HandleTick);
        _timer.Start();
    }

    public static void Ignore(Action tick)
    {
        Listeners.Remove(tick);
        if (Listeners.Count > 0 || _timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer = null;
    }

    private static double CurrentBeat()
    {
        if (_beat is not null)
        {
            return _beat();
        }

        return Clock.Elapsed.TotalSeconds * RestingBeatsPerSecond;
    }

    private static void HandleTick(object? sender, EventArgs e)
    {
        Action[] listeners = [.. Listeners];
        foreach (Action listener in listeners)
        {
            listener();
        }
    }
}
