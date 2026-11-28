using System.Diagnostics;

using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Devices;

/// <summary>
/// Times a device's audio thread without allocating or locking: when each callback began, so a late one (more than one and a half
/// buffers after the last, as when a collection or a busy machine held the thread) is counted, and how long the mixer took, against
/// the sound it made. Another thread takes the report, which starts the next one afresh.
/// </summary>
public sealed class AudioTiming(int bufferFrames)
{
    private const double LateBuffers = 1.5;

    private readonly long _lateTicks = (long)(bufferFrames * LateBuffers * Stopwatch.Frequency / AudioFormat.SampleRate);
    private long _lastBegin;
    private long _callbacks;
    private long _late;
    private long _longestGap;
    private long _longestRender;
    private long _renderTicks;
    private long _frames;

    /// <summary>Marks a callback beginning now and gives the time, for <see cref="End(long, int)"/>.</summary>
    public long Begin()
    {
        long now = Stopwatch.GetTimestamp();
        Begin(now);
        return now;
    }

    /// <summary>Marks a callback beginning at a time of <see cref="Stopwatch"/>'s.</summary>
    public void Begin(long timestamp)
    {
        long last = _lastBegin;
        _lastBegin = timestamp;
        if (last == 0)
        {
            return;
        }

        long gap = timestamp - last;
        RaiseTo(ref _longestGap, gap);
        if (gap > _lateTicks)
        {
            Interlocked.Increment(ref _late);
        }
    }

    /// <summary>Marks the callback begun at <paramref name="started"/> done now, having filled <paramref name="frames"/>.</summary>
    public void End(long started, int frames) => End(started, Stopwatch.GetTimestamp(), frames);

    public void End(long started, long ended, int frames)
    {
        long render = ended - started;
        Interlocked.Increment(ref _callbacks);
        Interlocked.Add(ref _renderTicks, render);
        Interlocked.Add(ref _frames, frames);
        RaiseTo(ref _longestRender, render);
    }

    /// <summary>The report since the last one.</summary>
    public AudioTimingReport Take()
    {
        long callbacks = Interlocked.Exchange(ref _callbacks, 0);
        long late = Interlocked.Exchange(ref _late, 0);
        long gap = Interlocked.Exchange(ref _longestGap, 0);
        long render = Interlocked.Exchange(ref _longestRender, 0);
        long renderTicks = Interlocked.Exchange(ref _renderTicks, 0);
        long frames = Interlocked.Exchange(ref _frames, 0);
        double soundSeconds = frames / (double)AudioFormat.SampleRate;
        double renderSeconds = renderTicks / (double)Stopwatch.Frequency;
        double load = soundSeconds > 0 ? renderSeconds / soundSeconds : 0;
        TimeSpan longestGap = Stopwatch.GetElapsedTime(0, gap);
        TimeSpan longestRender = Stopwatch.GetElapsedTime(0, render);
        return new AudioTimingReport(callbacks, late, longestGap, longestRender, load);
    }

    private static void RaiseTo(ref long field, long value)
    {
        long seen = Interlocked.Read(ref field);
        while (value > seen)
        {
            long previous = Interlocked.CompareExchange(ref field, value, seen);
            if (previous == seen)
            {
                return;
            }

            seen = previous;
        }
    }
}
