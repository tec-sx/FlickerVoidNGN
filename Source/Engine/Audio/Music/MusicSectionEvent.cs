namespace FlickerVoid.Engine.Audio.Music;

/// <summary>Sent as the music heard enters a <see cref="MusicSection"/> of its cue after the first; <paramref name="Bar"/> is the bar of the cue the section begins on, counted from 1.</summary>
public readonly record struct MusicSectionEvent(string Cue, string Section, int Bar);
