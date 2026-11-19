namespace FlickerVoid.Engine.Runtime.Loop;

/// <summary>Timing of one update: the time it covers, the total time including it, and how many updates of its kind ran, including it.</summary>
public readonly record struct GameTime(TimeSpan Elapsed, TimeSpan Total, long Count);

