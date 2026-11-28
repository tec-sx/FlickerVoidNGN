using System.ComponentModel.DataAnnotations;

using FlickerVoid.Engine.Runtime.Components;

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

    /// <summary>The name of the track it belongs to; it is sent only while that track sounds; empty for a point of the whole cue.</summary>
    public string Track { get; set; } = string.Empty;

    /// <summary>Whether it sits at its bar of every phrase instead of once.</summary>
    public bool Repeat { get; set; }

    /// <summary>A sound file played once from its bar, a single shot on the grid, on its track's bus; empty for none.</summary>
    [ContentFile(".wav", ".ogg")]
    public string Sound { get; set; } = string.Empty;

    /// <summary>The gain of its sound.</summary>
    public float Gain { get; set; } = 1;
}
