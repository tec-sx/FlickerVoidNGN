using System.Globalization;

using FlickerVoid.Engine.Audio.Devices;
using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>
/// What the music editor says, once a second, about how its sound kept up: the share of the audio thread's time mixing took, the
/// calls of the device that came late since playing began (each a likely crackle), and how far the master's limiter turned the mix
/// down; its tip adds the longest wait against the buffer and the time the runtime paused for collections.
/// </summary>
public sealed class AudioReadout(int bufferFrames)
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    private const double LateBuffers = 1.5;

    private TimeSpan _elapsed;
    private long _callbacks;
    private double _loadSum;
    private long _late;
    private TimeSpan _longestGap;
    private TimeSpan _collections;
    private float _limit = 1;

    public string Text { get; private set; } = string.Empty;

    public string Tip { get; private set; } = string.Empty;

    /// <summary>Starts the count of late calls again, as playing starts.</summary>
    public void Reset()
    {
        _late = 0;
        Clear();
    }

    /// <summary>Adds what passed since the last call; true when a second has gathered and the text changed.</summary>
    public bool Add(AudioTimingReport report, TimeSpan collections, float limit, TimeSpan elapsed)
    {
        _elapsed += elapsed;
        _callbacks += report.Callbacks;
        _loadSum += report.Load * report.Callbacks;
        _late += report.Late;
        _collections += collections;
        _limit = MathF.Min(_limit, limit);
        if (report.LongestGap > _longestGap)
        {
            _longestGap = report.LongestGap;
        }

        if (_elapsed < Window)
        {
            return false;
        }

        Show();
        Clear();
        return true;
    }

    private void Show()
    {
        double load = _callbacks > 0 ? _loadSum / _callbacks : 0;
        string mix = (load * 100).ToString("0.0", CultureInfo.InvariantCulture);
        string text = $"mix {mix}% · {_late} late";
        string limited = "The limiter did not need to turn the mix down.";
        if (_limit < 0.999f)
        {
            double decibels = 20 * Math.Log10(_limit);
            string cut = decibels.ToString("0.0", CultureInfo.InvariantCulture);
            text += $" · limit {cut} dB";
            limited = $"The master's limiter turned the mix down by up to {cut} dB, so it does not clip: lower the layers to leave it headroom.";
        }

        double buffer = bufferFrames * 1000.0 / AudioFormat.SampleRate;
        double late = buffer * LateBuffers;
        string waited = _longestGap.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture);
        string held = _collections.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture);
        Text = text;
        Tip = $"In the last second the audio thread waited up to {waited} ms between calls (its buffer holds {buffer:0} ms; a wait past {late:0} ms may be heard as a crackle), "
            + $"mixing took {mix}% of its time, and the runtime paused the program {held} ms for collections. {_late} calls came late since playing began. {limited}";
    }

    private void Clear()
    {
        _elapsed = TimeSpan.Zero;
        _callbacks = 0;
        _loadSum = 0;
        _longestGap = TimeSpan.Zero;
        _collections = TimeSpan.Zero;
        _limit = 1;
    }
}
