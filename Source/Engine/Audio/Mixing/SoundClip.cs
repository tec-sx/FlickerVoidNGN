namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>A sound file decoded into memory: interleaved samples of one or two channels at the file's own rate.</summary>
public sealed class SoundClip
{
    public SoundClip(float[] samples, int channels, int sampleRate)
    {
        if (channels is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(channels), channels, "A clip has one or two channels.");
        }

        Samples = samples;
        Channels = channels;
        SampleRate = sampleRate;
        Frames = samples.Length / channels;
    }

    public float[] Samples { get; }

    public int Channels { get; }

    public int SampleRate { get; }

    public int Frames { get; }

    public double Seconds => Frames / (double)SampleRate;
}
