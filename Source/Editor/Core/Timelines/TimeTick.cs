namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>A line of a time scale at a time, with its label when it has one.</summary>
public readonly record struct TimeTick(double Time, TickLevel Level, string? Label);
