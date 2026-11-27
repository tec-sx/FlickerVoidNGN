namespace FlickerVoid.Engine.Audio.Music;

/// <summary>Where a cue is on its grid: the bar and beat counted from 1, and the bars played so far with the fraction of the bar under way.</summary>
public readonly record struct MusicPosition(int Bar, int Beat, double Bars);
