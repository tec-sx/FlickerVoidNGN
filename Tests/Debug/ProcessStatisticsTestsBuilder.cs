using NSubstitute;

namespace FlickerVoid.Engine.Debug.Tests;

/// <summary>Builds process statistics over a probe whose clock and process figures the test sets.</summary>
internal sealed class ProcessStatisticsTestsBuilder
{
    private readonly IProcessProbe _probe = Substitute.For<IProcessProbe>();
    private ProcessSample _sample = new(TimeSpan.Zero, TimeSpan.Zero, 0, 0, 0, 0, 0);
    private int _cores = 1;

    public ProcessStatisticsTestsBuilder WithCores(int cores)
    {
        _cores = cores;
        return this;
    }

    public ProcessStatisticsTestsBuilder WithMemory(long workingSet, long managed)
    {
        _sample = _sample with { WorkingSetBytes = workingSet, ManagedBytes = managed };
        return this;
    }

    public ProcessStatisticsTestsBuilder WithCollections(int gen0, int gen1, int gen2)
    {
        _sample = _sample with { Gen0 = gen0, Gen1 = gen1, Gen2 = gen2 };
        return this;
    }

    /// <summary>Moves the clock and the process's CPU time on together.</summary>
    public void Advance(TimeSpan wall, TimeSpan cpu)
    {
        _sample = _sample with { Wall = _sample.Wall + wall, Cpu = _sample.Cpu + cpu };
    }

    public ProcessStatistics Build()
    {
        _probe.ProcessorCount.Returns(_cores);
        _probe.Read().Returns(_ => _sample);
        return new ProcessStatistics(_probe);
    }
}
