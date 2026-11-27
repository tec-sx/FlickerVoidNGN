namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>
/// The average time a render pass, or a GPU timer inside one, took per frame on the CPU and on the GPU, in milliseconds; its
/// depth counts the timers it is nested in.
/// </summary>
public readonly record struct PassTiming(string Name, int Depth, double CpuMilliseconds, double GpuMilliseconds);
