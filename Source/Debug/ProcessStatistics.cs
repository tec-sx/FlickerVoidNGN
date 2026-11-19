namespace FlickerVoid.Engine.Debug;

/// <summary>The process's CPU use as a share of all cores since the last update, its memory and its garbage collections.</summary>
public sealed class ProcessStatistics
{
    private readonly IProcessProbe _probe;
    private ProcessSample _previous;
    private int[] _collections = [0, 0, 0];

    public ProcessStatistics(IProcessProbe probe)
    {
        _probe = probe;
        _previous = probe.Read();
    }

    public float CpuPercent { get; private set; }

    public long WorkingSetBytes { get; private set; }

    public long ManagedBytes { get; private set; }

    /// <summary>The collections so far of generation 0, 1 and 2.</summary>
    public IReadOnlyList<int> Collections => _collections;

    public void Update()
    {
        ProcessSample sample = _probe.Read();
        TimeSpan wall = sample.Wall - _previous.Wall;
        if (wall > TimeSpan.Zero)
        {
            TimeSpan cpu = sample.Cpu - _previous.Cpu;
            CpuPercent = (float)(cpu / (wall * _probe.ProcessorCount) * 100);
            _previous = sample;
        }

        WorkingSetBytes = sample.WorkingSetBytes;
        ManagedBytes = sample.ManagedBytes;
        _collections = [sample.Gen0, sample.Gen1, sample.Gen2];
    }
}
