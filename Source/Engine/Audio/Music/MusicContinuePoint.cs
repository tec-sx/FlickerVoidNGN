using System.ComponentModel.DataAnnotations;

namespace FlickerVoid.Engine.Audio.Music;

/// <summary>A named bar a cue can continue from when it is taken back from under another cue.</summary>
public sealed class MusicContinuePoint
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The bar, counted from 1.</summary>
    [Range(1, 65)]
    public float Bar { get; set; } = 1;
}
