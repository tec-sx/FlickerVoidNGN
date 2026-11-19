namespace FlickerVoid.Engine.Debug;

/// <summary>What a process probe reads at one moment: the clock and the process's CPU time, memory and garbage collections.</summary>
public readonly record struct ProcessSample(TimeSpan Wall, TimeSpan Cpu, long WorkingSetBytes, long ManagedBytes, int Gen0, int Gen1, int Gen2);
