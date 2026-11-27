namespace FlickerVoid.Engine.Graphics.Commands;

/// <summary>
/// How long the GPU took over the work recorded inside a named timer, and when it started, in milliseconds after the first timer
/// of its submission started; its depth counts the timers it is nested in.
/// </summary>
public readonly record struct GpuTiming(string Name, int Depth, double StartMilliseconds, double Milliseconds);
