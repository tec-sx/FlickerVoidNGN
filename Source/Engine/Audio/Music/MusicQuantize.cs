namespace FlickerVoid.Engine.Audio.Music;

/// <summary>The line of a cue's grid a change waits for; a cue without a tempo has no grid, so every change is at once.</summary>
public enum MusicQuantize
{
    None,
    Beat,
    Bar,
    Phrase,
}
