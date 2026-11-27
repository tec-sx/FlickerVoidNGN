namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>
/// Changes a clip's length without changing its pitch, by waveform similarity overlap-add: windows of the clip are laid out at the
/// new spacing, each shifted a little to the place that continues the last one best. Done once when a loop is prepared, never on
/// the audio thread.
/// </summary>
public static class TimeStretch
{
    private const int Window = 2048;
    private const int Hop = Window / 2;
    private const int Search = 384;
    private const int Step = 4;

    /// <summary>A clip <paramref name="ratio"/> times as long (2 is twice as slow), at the clip's own rate and channels.</summary>
    public static SoundClip Stretch(SoundClip clip, double ratio)
    {
        if (ratio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "A stretch ratio is above zero.");
        }

        int channels = clip.Channels;
        int inFrames = clip.Frames;
        int outFrames = Math.Max(1, (int)Math.Round(inFrames * ratio));
        if (Math.Abs(ratio - 1) < 1e-6 || inFrames < Window * 2)
        {
            return ratio == 1 ? clip : Resample(clip, outFrames);
        }

        float[] mono = MixDown(clip);
        float[] window = CreateWindow();
        float[] output = new float[(outFrames + Window) * channels];
        double analysisHop = Hop / ratio;
        int previous = 0;
        double nominal = 0;
        for (int outStart = 0; outStart < outFrames; outStart += Hop)
        {
            int start;
            if (outStart == 0)
            {
                start = 0;
            }
            else
            {
                int target = previous + Hop;
                start = FindBest(mono, (int)Math.Round(nominal), target);
            }

            for (int frame = 0; frame < Window; frame++)
            {
                int source = start + frame;
                float weight = window[frame];
                for (int channel = 0; channel < channels; channel++)
                {
                    float value = source < inFrames ? clip.Samples[(source * channels) + channel] : 0;
                    output[((outStart + frame) * channels) + channel] += value * weight;
                }
            }

            previous = start;
            nominal += analysisHop;
        }

        float[] trimmed = new float[outFrames * channels];
        Array.Copy(output, trimmed, trimmed.Length);
        return new SoundClip(trimmed, channels, clip.SampleRate);
    }

    private static int FindBest(float[] mono, int nominal, int target)
    {
        int best = Math.Clamp(nominal, 0, Math.Max(0, mono.Length - Window));
        if (target + Window > mono.Length)
        {
            return best;
        }

        double bestScore = double.MinValue;
        int low = Math.Max(0, nominal - Search);
        int high = Math.Min(mono.Length - Window, nominal + Search);
        for (int candidate = low; candidate <= high; candidate += 2)
        {
            double score = 0;
            for (int frame = 0; frame < Window; frame += Step)
            {
                score += mono[candidate + frame] * mono[target + frame];
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static float[] CreateWindow()
    {
        float[] window = new float[Window];
        for (int frame = 0; frame < Window; frame++)
        {
            window[frame] = (float)(0.5 - (0.5 * Math.Cos(2 * Math.PI * frame / Window)));
        }

        return window;
    }

    private static float[] MixDown(SoundClip clip)
    {
        float[] mono = new float[clip.Frames];
        for (int frame = 0; frame < mono.Length; frame++)
        {
            float sum = 0;
            for (int channel = 0; channel < clip.Channels; channel++)
            {
                sum += clip.Samples[(frame * clip.Channels) + channel];
            }

            mono[frame] = sum / clip.Channels;
        }

        return mono;
    }

    private static SoundClip Resample(SoundClip clip, int frames)
    {
        float[] samples = new float[frames * clip.Channels];
        for (int frame = 0; frame < frames; frame++)
        {
            int source = Math.Min(clip.Frames - 1, (int)((long)frame * clip.Frames / frames));
            for (int channel = 0; channel < clip.Channels; channel++)
            {
                samples[(frame * clip.Channels) + channel] = clip.Samples[(source * clip.Channels) + channel];
            }
        }

        return new SoundClip(samples, clip.Channels, clip.SampleRate);
    }
}
