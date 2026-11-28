using System.Diagnostics;

using FlickerVoid.Engine.Audio.Devices;

namespace FlickerVoid.Engine.Audio.Tests.Devices;

public sealed class AudioTimingTests
{
    private readonly AudioTimingTestsBuilder _builder = new();

    [Fact]
    public void CountsTheCallbacksAndHowMuchOfTheirTimeMixingTook()
    {
        AudioTiming timing = _builder.Build(bufferFrames: 960);

        _builder.Call(timing, renderMilliseconds: 2);
        _builder.Call(timing, renderMilliseconds: 4);

        AudioTimingReport report = timing.Take();
        report.Callbacks.Should().Be(2);
        report.LongestRender.TotalMilliseconds.Should().BeApproximately(4, 0.01);
        report.Load.Should().BeApproximately(0.15, 0.001, "6 ms of mixing for 40 ms of sound");
        report.Late.Should().Be(0);
    }

    [Fact]
    public void CountsACallbackThatCameLateAsAStall()
    {
        AudioTiming timing = _builder.Build(bufferFrames: 960);
        _builder.Call(timing, renderMilliseconds: 1);

        _builder.Wait(35);
        _builder.Call(timing, renderMilliseconds: 1);

        AudioTimingReport report = timing.Take();
        report.Late.Should().Be(1, "the device asked 55 ms after its last call, where 20 ms of sound was queued");
        report.LongestGap.TotalMilliseconds.Should().BeApproximately(55, 0.01);
    }

    [Fact]
    public void StartsAfreshAfterEachReport()
    {
        AudioTiming timing = _builder.Build(bufferFrames: 960);
        _builder.Call(timing, renderMilliseconds: 3);
        timing.Take();

        _builder.Call(timing, renderMilliseconds: 1);

        AudioTimingReport report = timing.Take();
        report.Callbacks.Should().Be(1);
        report.LongestRender.TotalMilliseconds.Should().BeApproximately(1, 0.01);
    }

    [Fact]
    public void ReadsTheClockItIsGiven()
    {
        long before = Stopwatch.GetTimestamp();
        AudioTiming timing = new(1024);

        long started = timing.Begin();
        timing.End(started, 1024);

        timing.Take().Callbacks.Should().Be(1);
        started.Should().BeGreaterThanOrEqualTo(before);
    }
}
