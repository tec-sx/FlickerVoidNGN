namespace FlickerVoid.Engine.Runtime.Loop;

/// <summary>Turns elapsed real time into fixed steps for simulation and one variable step per frame; the stepping rules are adapted from Stride's game loop.</summary>
public sealed class GameLoop
{
    public static readonly TimeSpan DefaultFixedStep = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    public static readonly TimeSpan DefaultMaxElapsed = TimeSpan.FromMilliseconds(500);

    private TimeSpan _accumulated;
    private TimeSpan _fixedTotal;
    private TimeSpan _frameTotal;
    private long _fixedCount;
    private long _frameCount;

    /// <summary>Raised once per fixed step, with Elapsed always equal to FixedStep.</summary>
    public event Action<GameTime>? FixedUpdate;

    /// <summary>Raised once per frame, after that frame's fixed steps.</summary>
    public event Action<GameTime>? Update;

    public TimeSpan FixedStep { get; init; } = DefaultFixedStep;

    /// <summary>Longest frame the loop accepts; a longer one (a hitch or a breakpoint) counts as this long, so the simulation doesn't try to catch up.</summary>
    public TimeSpan MaxElapsed { get; init; } = DefaultMaxElapsed;

    /// <summary>Fraction of the next fixed step already elapsed, for drawing between the last two fixed states.</summary>
    public float Interpolation => (float)((double)_accumulated.Ticks / FixedStep.Ticks);

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }
        else if (elapsed > MaxElapsed)
        {
            elapsed = MaxElapsed;
        }

        // A frame within 1/64 of a step counts as exactly one step, so timer noise doesn't skip or double steps.
        TimeSpan stepTime = elapsed;
        long offset = Math.Abs(elapsed.Ticks - FixedStep.Ticks);
        if (offset < (FixedStep.Ticks >> 6))
        {
            stepTime = FixedStep;
        }

        _accumulated += stepTime;
        while (_accumulated >= FixedStep)
        {
            _accumulated -= FixedStep;
            _fixedTotal += FixedStep;
            _fixedCount++;
            GameTime step = new(FixedStep, _fixedTotal, _fixedCount);
            FixedUpdate?.Invoke(step);
        }

        _frameTotal += elapsed;
        _frameCount++;
        GameTime frame = new(elapsed, _frameTotal, _frameCount);
        Update?.Invoke(frame);
    }
}

