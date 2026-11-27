using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Tests.Mixing;

public sealed class ClipSourceTests
{
    [Fact]
    public void PlaysAMonoClipToBothEars()
    {
        SoundClip clip = new([0.1f, 0.2f], 1, AudioFormat.SampleRate);
        ClipSource source = new(clip);
        float[] heard = new float[8];

        int read = source.Read(heard);

        read.Should().Be(2);
        heard.Take(4).Should().Equal(0.1f, 0.1f, 0.2f, 0.2f);
    }

    [Fact]
    public void KeepsAStereoClipsSides()
    {
        SoundClip clip = new([0.1f, 0.9f, 0.2f, 0.8f], 2, AudioFormat.SampleRate);
        ClipSource source = new(clip);
        float[] heard = new float[4];

        source.Read(heard);

        heard.Should().Equal(0.1f, 0.9f, 0.2f, 0.8f);
    }

    [Fact]
    public void LoopsWithoutEnding()
    {
        SoundClip clip = new([0.1f, 0.2f, 0.3f], 1, AudioFormat.SampleRate);
        ClipSource source = new(clip, loop: true);
        float[] heard = new float[14];

        int read = source.Read(heard);

        read.Should().Be(7);
        heard[12].Should().Be(0.1f);
    }

    [Fact]
    public void StretchesAClipOfHalfTheRateToTwiceTheFrames()
    {
        SoundClip clip = new([0, 1, 0, 1], 1, AudioFormat.SampleRate / 2);
        ClipSource source = new(clip);
        float[] heard = new float[32];

        int read = source.Read(heard);

        read.Should().Be(8);
        heard[2].Should().BeApproximately(0.5f, 0.0001f);
    }

    [Fact]
    public void PlaysTwiceAsFastAtTwiceThePitch()
    {
        float[] samples = new float[100];
        SoundClip clip = new(samples, 1, AudioFormat.SampleRate);
        ClipSource source = new(clip, pitch: 2);
        float[] heard = new float[400];

        int read = source.Read(heard);

        read.Should().Be(50);
    }
}
