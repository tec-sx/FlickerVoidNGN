namespace FlickerVoid.Engine.Audio.Music;

/// <summary>Sent as the music heard reaches a beat (the latest, when a frame passed more than one), the bar and beat counted from 1.</summary>
public readonly record struct MusicBeatEvent(string Cue, int Bar, int Beat);
