namespace FlickerVoid.Engine.Runtime.Loop;

/// <summary>
/// Timing of one update: the time it covers, the total time including it, how many updates of its kind ran, including it, and whether
/// the game is paused, when frames go on without fixed steps and what animates the world holds still.
/// </summary>
public readonly record struct GameTime(TimeSpan Elapsed, TimeSpan Total, long Count, bool Paused = false);

