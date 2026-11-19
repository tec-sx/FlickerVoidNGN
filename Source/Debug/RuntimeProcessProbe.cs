using System.Diagnostics;

namespace FlickerVoid.Engine.Debug;

/// <summary>Reads the current process through System.Diagnostics and the garbage collector.</summary>
public sealed class RuntimeProcessProbe : IProcessProbe
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public int ProcessorCount => Environment.ProcessorCount;

    public ProcessSample Read()
    {
        _process.Refresh();
        long managed = GC.GetTotalMemory(forceFullCollection: false);
        return new ProcessSample(
            _clock.Elapsed,
            _process.TotalProcessorTime,
            _process.WorkingSet64,
            managed,
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2));
    }
}
