using System.Diagnostics;

using FlickerVoid.Engine.Audio.Devices;

namespace FlickerVoid.Engine.Audio.Tests.Devices;

/// <summary>
/// Builds a device's timing and drives it on a clock of its own: each call comes one buffer after the last, unless the test waits
/// longer between them, and takes the milliseconds of mixing it is given.
/// </summary>
internal sealed class AudioTimingTestsBuilder
{
    private int _bufferFrames;
    private long _now = Stopwatch.Frequency;

    public AudioTiming Build(int bufferFrames)
    {
        _bufferFrames = bufferFrames;
        return new AudioTiming(bufferFrames);
    }

    public void Wait(double milliseconds) => _now += ToTicks(milliseconds);

    /// <summary>A callback: begins now, mixes for the time given, and the next one is due a buffer after this one began.</summary>
    public void Call(AudioTiming timing, double renderMilliseconds)
    {
        long started = _now;
        timing.Begin(started);
        long ended = started + ToTicks(renderMilliseconds);
        timing.End(started, ended, _bufferFrames);
        double buffer = _bufferFrames * 1000.0 / 48000;
        _now = started + ToTicks(buffer);
    }

    private static long ToTicks(double milliseconds) => (long)Math.Round(milliseconds * Stopwatch.Frequency / 1000);
}
