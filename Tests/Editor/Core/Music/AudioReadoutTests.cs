using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Engine.Audio.Devices;

namespace FlickerVoid.Editor.Core.Tests.Music;

public sealed class AudioReadoutTests
{
    private readonly AudioReadoutTestsBuilder _builder = new();

    [Fact]
    public void SaysHowMuchOfTheAudioThreadMixingTakesOncePerSecond()
    {
        AudioReadout readout = _builder.Build();

        bool shown = readout.Add(AudioReadoutTestsBuilder.Report(load: 0.004), TimeSpan.Zero, 1, TimeSpan.FromSeconds(0.5));
        shown.Should().BeFalse("the readout changes once a second, so it can be read");
        shown = readout.Add(AudioReadoutTestsBuilder.Report(load: 0.006), TimeSpan.Zero, 1, TimeSpan.FromSeconds(0.5));

        shown.Should().BeTrue();
        readout.Text.Should().Be("mix 0.5% · 0 late");
    }

    [Fact]
    public void KeepsCountingLateCallsUntilReset()
    {
        AudioReadout readout = _builder.Build();
        readout.Add(AudioReadoutTestsBuilder.Report(late: 2), TimeSpan.Zero, 1, TimeSpan.FromSeconds(1));

        readout.Add(AudioReadoutTestsBuilder.Report(late: 1), TimeSpan.Zero, 1, TimeSpan.FromSeconds(1));

        readout.Text.Should().Be("mix 0.4% · 3 late");
        readout.Reset();
        readout.Add(AudioReadoutTestsBuilder.Report(), TimeSpan.Zero, 1, TimeSpan.FromSeconds(1));
        readout.Text.Should().Be("mix 0.4% · 0 late");
    }

    [Fact]
    public void ShowsHowFarTheLimiterTurnedTheMixDown()
    {
        AudioReadout readout = _builder.Build();

        readout.Add(AudioReadoutTestsBuilder.Report(), TimeSpan.Zero, 0.5f, TimeSpan.FromSeconds(1));

        readout.Text.Should().Be("mix 0.4% · 0 late · limit -6.0 dB");
    }

    [Fact]
    public void ExplainsTheWaitsAndTheCollectionsInItsTip()
    {
        AudioReadout readout = _builder.Build(bufferFrames: 1024);

        readout.Add(AudioReadoutTestsBuilder.Report(longestGap: 45), TimeSpan.FromMilliseconds(12), 1, TimeSpan.FromSeconds(1));

        readout.Tip.Should().Contain("waited up to 45 ms").And.Contain("its buffer holds 21 ms").And.Contain("12 ms");
    }
}
