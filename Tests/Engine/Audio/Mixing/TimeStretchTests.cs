using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Tests.Mixing;

public sealed class TimeStretchTests
{
    private static SoundClip Sine(double hertz, double seconds)
    {
        int frames = (int)(seconds * AudioFormat.SampleRate);
        float[] samples = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            samples[frame] = (float)(0.5 * Math.Sin(2 * Math.PI * hertz * frame / AudioFormat.SampleRate));
        }

        return new SoundClip(samples, 1, AudioFormat.SampleRate);
    }

    private static double MeasureHertz(SoundClip clip, int from, int frames)
    {
        int crossings = 0;
        for (int frame = from + 1; frame < from + frames; frame++)
        {
            if (clip.Samples[frame - 1] < 0 && clip.Samples[frame] >= 0)
            {
                crossings++;
            }
        }

        return crossings * (double)clip.SampleRate / frames;
    }

    [Theory]
    [InlineData(0.9)]
    [InlineData(1.25)]
    public void ChangesTheLengthByTheRatioAndKeepsThePitch(double ratio)
    {
        SoundClip clip = Sine(440, 2);

        SoundClip stretched = TimeStretch.Stretch(clip, ratio);

        stretched.Frames.Should().Be((int)Math.Round(clip.Frames * ratio));
        MeasureHertz(stretched, 20000, 48000).Should().BeApproximately(440, 12);
    }

    [Fact]
    public void KeepsTheLoudnessOfTheMiddle()
    {
        SoundClip stretched = TimeStretch.Stretch(Sine(220, 2), 1.1);

        float peak = stretched.Samples.Skip(30000).Take(20000).Max(Math.Abs);

        peak.Should().BeInRange(0.45f, 0.55f);
    }

    [Fact]
    public void ReturnsTheSameClipForARatioOfOne()
    {
        SoundClip clip = Sine(440, 1);

        TimeStretch.Stretch(clip, 1).Should().BeSameAs(clip);
    }
}
