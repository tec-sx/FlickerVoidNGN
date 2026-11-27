using System.ComponentModel.DataAnnotations;

namespace FlickerVoid.Engine.Audio.Music;

/// <summary>
/// A named point on a cue's phrase that sends a <see cref="MusicNotifyEvent"/> every time the music passes it, so gameplay can
/// act on the beat of a song: flash a light on the snare, open a door on the drop.
/// </summary>
public sealed class MusicNotify
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The bar of the phrase it sits on, counted from 1, with fractions (2.5 is the third beat of a four-beat bar's second bar).</summary>
    [Range(1, 65)]
    public float Bar { get; set; } = 1;
}
