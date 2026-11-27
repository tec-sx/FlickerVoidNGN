namespace FlickerVoid.Engine.Audio.Music;

/// <summary>A cue on a bus's stack: whether a cue above it holds it, and the bars it has played (where it stopped, when held).</summary>
public readonly record struct MusicStackEntry(string Cue, bool IsHeld, double HeldBars, double Bars, int Layers);
