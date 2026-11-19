namespace FlickerVoid.Engine.Debug.Tests;

public sealed class FrameStatisticsTests
{
    private readonly FrameStatisticsTestsBuilder _builder = new();

    [Fact]
    public void ReportsZeroBeforeAnyFrame()
    {
        FrameStatistics statistics = _builder.Build();

        statistics.FramesPerSecond.Should().Be(0);
        statistics.AverageMilliseconds.Should().Be(0);
        statistics.History.Length.Should().Be(0);
    }

    [Fact]
    public void AveragesTheRecentFrames()
    {
        FrameStatistics statistics = _builder.WithFrames(0.010f, 0.020f, 0.030f).Build();

        statistics.AverageMilliseconds.Should().BeApproximately(20, 1e-3f);
        statistics.FramesPerSecond.Should().BeApproximately(50, 1e-2f);
        statistics.MaxMilliseconds.Should().BeApproximately(30, 1e-3f);
    }

    [Fact]
    public void ForgetsFramesOlderThanItsHistory()
    {
        FrameStatistics statistics = _builder.WithRepeatedFrames(FrameStatistics.HistoryLength, 0.100f).WithFrames(0.010f).Build();

        statistics.History.Length.Should().Be(FrameStatistics.HistoryLength);
        statistics.MaxMilliseconds.Should().BeApproximately(100, 1e-3f);
        statistics.History[^1].Should().BeApproximately(10, 1e-3f);
    }

    [Fact]
    public void ForgetsTheMaximumOnceItLeavesTheHistory()
    {
        FrameStatistics statistics = _builder.WithFrames(0.500f).WithRepeatedFrames(FrameStatistics.HistoryLength, 0.010f).Build();

        statistics.MaxMilliseconds.Should().BeApproximately(10, 1e-3f);
    }

    [Fact]
    public void ListsTheHistoryOldestFirst()
    {
        FrameStatistics statistics = _builder.WithFrames(0.010f, 0.020f, 0.030f).Build();

        statistics.History.ToArray().Should().Equal(10f, 20f, 30f);
    }
}
