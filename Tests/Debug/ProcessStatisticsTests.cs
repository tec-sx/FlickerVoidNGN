namespace FlickerVoid.Engine.Debug.Tests;

public sealed class ProcessStatisticsTests
{
    private readonly ProcessStatisticsTestsBuilder _builder = new();

    [Fact]
    public void ReportsTheProcessCpuTimeAsAShareOfAllCores()
    {
        ProcessStatistics statistics = _builder.WithCores(4).Build();

        _builder.Advance(wall: TimeSpan.FromSeconds(1), cpu: TimeSpan.FromSeconds(0.5));
        statistics.Update();

        statistics.CpuPercent.Should().BeApproximately(12.5f, 1e-3f);
    }

    [Fact]
    public void KeepsTheLastShareWhenNoTimeHasPassed()
    {
        ProcessStatistics statistics = _builder.WithCores(1).Build();
        _builder.Advance(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.25));
        statistics.Update();

        statistics.Update();

        statistics.CpuPercent.Should().BeApproximately(25, 1e-3f);
    }

    [Fact]
    public void ReportsMemoryAndCollectionsFromTheLatestSample()
    {
        ProcessStatistics statistics = _builder.WithMemory(workingSet: 300, managed: 100).WithCollections(3, 2, 1).Build();

        statistics.Update();

        statistics.WorkingSetBytes.Should().Be(300);
        statistics.ManagedBytes.Should().Be(100);
        statistics.Collections.Should().Equal(3, 2, 1);
    }
}
