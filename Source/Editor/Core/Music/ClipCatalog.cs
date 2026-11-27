using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Editor.Core.Music;

/// <summary>A sound file's length in seconds and its loudness at <see cref="ClipCatalog.PeakCount"/> places along it, from 0 to 1.</summary>
public sealed record ClipInfo(double Seconds, float[] Peaks);

/// <summary>Reads sound files once for the music editor, through <paramref name="load"/>, and keeps what it draws and measures from them.</summary>
public sealed class ClipCatalog(Func<string, SoundClip?> load)
{
    public const int PeakCount = 512;

    private readonly Dictionary<string, ClipInfo?> _known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The file's info; null for a file that is empty, missing or can't be read.</summary>
    public ClipInfo? Find(string file)
    {
        if (string.IsNullOrEmpty(file))
        {
            return null;
        }

        if (_known.TryGetValue(file, out ClipInfo? known))
        {
            return known;
        }

        SoundClip? clip = load(file);
        ClipInfo? info = clip is null ? null : Measure(clip);
        _known[file] = info;
        return info;
    }

    public void Forget() => _known.Clear();

    private static ClipInfo Measure(SoundClip clip)
    {
        float[] peaks = new float[PeakCount];
        int channels = clip.Channels;
        for (int place = 0; place < PeakCount; place++)
        {
            int from = (int)((long)clip.Frames * place / PeakCount);
            int to = (int)((long)clip.Frames * (place + 1) / PeakCount);
            float loudest = 0;
            for (int frame = from; frame < to; frame++)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    float sample = MathF.Abs(clip.Samples[(frame * channels) + channel]);
                    loudest = MathF.Max(loudest, sample);
                }
            }

            peaks[place] = MathF.Min(1, loudest);
        }

        return new ClipInfo(clip.Seconds, peaks);
    }
}
