using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Tests.Mixing;

public sealed class MixerTests
{
    private readonly MixerTestsBuilder _builder = new();

    [Fact]
    public void PlaysAVoiceAtItsGainToEachEar()
    {
        SoundClip clip = MixerTestsBuilder.Constant(1000, 0.5f);
        _builder.Play(clip, left: 1, right: 0.5f);

        float[] heard = _builder.Device.Render(4);

        heard.Should().Equal(0.5f, 0.25f, 0.5f, 0.25f, 0.5f, 0.25f, 0.5f, 0.25f);
    }

    [Fact]
    public void CountsTheFramesItRendered()
    {
        _builder.Device.Render(300);
        _builder.Device.Render(212);

        _builder.Mixer.Position.Should().Be(512);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(256)]
    [InlineData(1000)]
    public void StartsAScheduledVoiceOnItsExactFrame(int start)
    {
        SoundClip clip = MixerTestsBuilder.Constant(5000);
        _builder.Play(clip, startFrame: start);

        float[] heard = _builder.Device.Render(start + 10);

        heard[(start - 1) * 2].Should().Be(0);
        heard[start * 2].Should().Be(1);
        heard.Take(start * 2).Should().OnlyContain(sample => sample == 0);
    }

    [Fact]
    public void KeepsTwoVoicesScheduledOnOneFrameInStep()
    {
        SoundClip ramp = MixerTestsBuilder.Ramp(900);
        _builder.Play(ramp, left: 1, right: 0, startFrame: 700);
        _builder.Device.Render(100);
        _builder.Play(ramp, left: 0, right: 1, startFrame: 700);

        float[] heard = _builder.Device.Render(1000);

        for (int frame = 0; frame < 1000; frame++)
        {
            heard[frame * 2].Should().Be(heard[(frame * 2) + 1]);
        }
    }

    [Fact]
    public void ReportsAVoiceFinishedWhenItsSourceRunsOut()
    {
        SoundClip clip = MixerTestsBuilder.Constant(100);
        int voice = _builder.Play(clip);

        float[] heard = _builder.Device.Render(300);

        heard[99 * 2].Should().Be(1);
        heard[100 * 2].Should().Be(0);
        _builder.TakeFinished().Should().Equal(voice);
    }

    [Fact]
    public void FadesAStoppedVoiceOutAndEndsIt()
    {
        SoundClip clip = MixerTestsBuilder.Constant(10000);
        int voice = _builder.Play(clip);
        _builder.Device.Render(256);
        MixerCommand stop = MixerCommand.Stop(voice, 100);
        _builder.Mixer.Send(in stop);

        float[] heard = _builder.Device.Render(256);

        heard[0].Should().Be(1);
        heard[50 * 2].Should().BeApproximately(0.5f, 0.02f);
        heard[100 * 2].Should().Be(0);
        _builder.TakeFinished().Should().Equal(voice);
    }

    [Fact]
    public void ScalesABusByItsGainAndEverythingByTheMasters()
    {
        SoundClip clip = MixerTestsBuilder.Constant(10000);
        _builder.Play(clip, AudioBus.Music);
        _builder.Play(clip, AudioBus.Effects);
        MixerCommand music = MixerCommand.SetBus(AudioBus.Music, 0.5f);
        MixerCommand master = MixerCommand.SetBus(AudioBus.Master, 0.5f);
        _builder.Mixer.Send(in music);
        _builder.Mixer.Send(in master);

        float[] heard = _builder.Device.Render(512);

        heard[^1].Should().BeApproximately(0.75f, 0.0001f);
    }

    [Fact]
    public void RampsAGainChangeAcrossABlockSoItNeverClicks()
    {
        SoundClip clip = MixerTestsBuilder.Constant(10000);
        int voice = _builder.Play(clip);
        _builder.Device.Render(256);
        MixerCommand quiet = MixerCommand.SetVoice(voice, 0, 0);
        _builder.Mixer.Send(in quiet);

        float[] heard = _builder.Device.Render(256);

        float largestStep = 0;
        for (int frame = 1; frame < 256; frame++)
        {
            float step = MathF.Abs(heard[frame * 2] - heard[(frame - 1) * 2]);
            largestStep = MathF.Max(largestStep, step);
        }

        largestStep.Should().BeLessThan(0.01f);
        heard[^1].Should().BeApproximately(0, 0.0001f);
    }

    [Fact]
    public void HoldsAPausedBussVoicesWhereTheyAre()
    {
        SoundClip ramp = MixerTestsBuilder.Ramp(900);
        _builder.Play(ramp);
        _builder.Device.Render(256);
        MixerCommand pause = MixerCommand.PauseBus(AudioBus.Effects, true);
        _builder.Mixer.Send(in pause);

        float[] paused = _builder.Device.Render(256);
        MixerCommand resume = MixerCommand.PauseBus(AudioBus.Effects, false);
        _builder.Mixer.Send(in resume);
        float[] resumed = _builder.Device.Render(4);

        paused.Should().OnlyContain(sample => sample == 0);
        resumed[0].Should().BeApproximately(0.256f, 0.0001f);
    }

    [Fact]
    public void DullsAVoiceBelowItsCutoff()
    {
        SoundClip shrill = MixerTestsBuilder.Alternating(10000);
        _builder.Play(shrill, cutoff: 500);

        float[] heard = _builder.Device.Render(512);

        heard.Skip(512).Max().Should().BeLessThan(0.1f);
    }

    [Fact]
    public void DullsABusBelowItsCutoff()
    {
        SoundClip shrill = MixerTestsBuilder.Alternating(100000);
        _builder.Play(shrill, AudioBus.Ambience);
        MixerCommand dull = MixerCommand.SetBus(AudioBus.Ambience, 1, 500);
        _builder.Mixer.Send(in dull);

        float[] heard = _builder.Device.Render(48000);

        heard.Skip(90000).Max().Should().BeLessThan(0.1f);
    }

    [Fact]
    public void DropsAVoiceWhenNoneIsFreeAndSaysSo()
    {
        SoundClip clip = MixerTestsBuilder.Constant(10000, 0.001f);
        for (int index = 0; index < Mixer.MaxVoices; index++)
        {
            _builder.Play(clip);
        }

        int dropped = _builder.Play(clip);
        _builder.Device.Render(16);

        _builder.TakeFinished().Should().Equal(dropped);
    }

    [Fact]
    public void KeepsTheOutputWithinFullScale()
    {
        SoundClip clip = MixerTestsBuilder.Constant(1000);
        _builder.Play(clip);
        _builder.Play(clip);

        float[] heard = _builder.Device.Render(16);

        heard.Should().OnlyContain(sample => sample == 1);
    }

    [Fact]
    public void StopsAVoiceOnAScheduledFrame()
    {
        SoundClip clip = MixerTestsBuilder.Constant(5000);
        int voice = _builder.Play(clip);
        MixerCommand stop = MixerCommand.StopAt(voice, 700, 0);
        _builder.Mixer.Send(in stop);

        float[] heard = _builder.Device.Render(710);

        heard[699 * 2].Should().Be(1);
        heard[700 * 2].Should().Be(0);
    }

    [Fact]
    public void FadesAVoiceOutFromAScheduledFrame()
    {
        SoundClip clip = MixerTestsBuilder.Constant(5000);
        int voice = _builder.Play(clip);
        MixerCommand stop = MixerCommand.StopAt(voice, 300, 100);
        _builder.Mixer.Send(in stop);

        float[] heard = _builder.Device.Render(410);

        heard[299 * 2].Should().Be(1);
        heard[350 * 2].Should().BeApproximately(0.5f, 0.02f);
        heard[405 * 2].Should().Be(0);
    }

    [Fact]
    public void NeverSoundsAVoiceStoppedOnTheFrameItStarts()
    {
        SoundClip clip = MixerTestsBuilder.Constant(5000);
        int voice = _builder.Play(clip, startFrame: 400);
        MixerCommand stop = MixerCommand.StopAt(voice, 400, 0);
        _builder.Mixer.Send(in stop);

        float[] heard = _builder.Device.Render(600);

        heard.Should().OnlyContain(sample => sample == 0);
        _builder.Mixer.TryTakeFinished(out int finished).Should().BeTrue();
        finished.Should().Be(voice);
    }

    [Fact]
    public void FadesAVoiceInFromItsStart()
    {
        SoundClip clip = MixerTestsBuilder.Constant(5000);
        ClipSource source = new(clip);
        MixerCommand play = MixerCommand.PlayOnGrid(1, source, AudioBus.Music, 1, 100, 200);
        _builder.Mixer.Send(in play);

        float[] heard = _builder.Device.Render(400);

        heard[100 * 2].Should().Be(0);
        heard[200 * 2].Should().BeApproximately(0.5f, 0.02f);
        heard[350 * 2].Should().Be(1);
    }

    [Fact]
    public void StartsALateVoiceWhereItWouldHaveBeen()
    {
        SoundClip ramp = MixerTestsBuilder.Ramp(5000);
        _builder.Device.Render(1000);
        ClipSource source = new(ramp);
        MixerCommand play = MixerCommand.PlayOnGrid(1, source, AudioBus.Music, 1, 400);
        _builder.Mixer.Send(in play);

        float[] heard = _builder.Device.Render(4);

        heard[0].Should().BeApproximately(0.6f, 0.0001f);
    }
}
