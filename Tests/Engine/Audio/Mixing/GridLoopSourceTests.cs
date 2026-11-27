using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Tests.Mixing;

public sealed class GridLoopSourceTests
{
    [Fact]
    public void StartsOverEveryPeriod()
    {
        SoundClip ramp = MixerTestsBuilder.Ramp(100);
        GridLoopSource source = new(ramp, 100);
        float[] heard = new float[250 * 2];

        int read = source.Read(heard);

        read.Should().Be(250);
        heard[99 * 2].Should().BeApproximately(0.099f, 0.0001f);
        heard[100 * 2].Should().Be(0);
        heard[230 * 2].Should().BeApproximately(0.03f, 0.0001f);
    }

    [Fact]
    public void CutsAClipLongerThanThePeriod()
    {
        SoundClip ramp = MixerTestsBuilder.Ramp(150);
        GridLoopSource source = new(ramp, 100);
        float[] heard = new float[120 * 2];

        source.Read(heard);

        heard[110 * 2].Should().BeApproximately(0.01f, 0.0001f);
    }

    [Fact]
    public void IsSilentAfterAClipShorterThanThePeriod()
    {
        SoundClip clip = MixerTestsBuilder.Constant(60);
        GridLoopSource source = new(clip, 100);
        float[] heard = new float[120 * 2];

        source.Read(heard);

        heard[59 * 2].Should().Be(1);
        heard[80 * 2].Should().Be(0);
        heard[100 * 2].Should().Be(1);
    }

    [Fact]
    public void BeginsAtAnOffsetIntoThePeriod()
    {
        SoundClip ramp = MixerTestsBuilder.Ramp(100);
        GridLoopSource source = new(ramp, 100, 240);
        float[] heard = new float[4];

        source.Read(heard);

        heard[0].Should().BeApproximately(0.04f, 0.0001f);
    }
}
