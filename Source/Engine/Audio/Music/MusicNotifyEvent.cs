namespace FlickerVoid.Engine.Audio.Music;

/// <summary>Sent as the music heard passes one of its cue's <see cref="MusicNotify"/> points; <paramref name="Bar"/> is the bar of the cue, counted from 1, and <paramref name="Track"/> the track the point belongs to, empty for one of the whole cue.</summary>
public readonly record struct MusicNotifyEvent(string Cue, string Name, int Bar, string Track = "");
