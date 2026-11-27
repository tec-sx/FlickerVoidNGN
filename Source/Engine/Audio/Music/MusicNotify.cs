using System.ComponentModel.DataAnnotations;

namespace FlickerVoid.Engine.Audio.Music;

/// <summary>
/// A named point on a cue's timeline (a "cue" in the editor) that sends a <see cref="MusicNotifyEvent"/> when the music passes it,
/// so gameplay can act on the beat of a song: flash a light on the snare, open a door on the drop. It sits once at its bar, or at
/// that bar of every phrase when it repeats.
/// </summary>
public sealed class MusicNotify
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The bar of the cue it sits on, counted from 1, with fractions (2.5 is the third beat of a four-beat bar's second bar); with <see cref="Repeat"/>, the bar of the phrase.</summary>
    [Range(1, 4096)]
    public float Bar { get; set; } = 1;

    /// <summary>Whether it sits at its bar of every phrase instead of once.</summary>
    public bool Repeat { get; set; }
}
