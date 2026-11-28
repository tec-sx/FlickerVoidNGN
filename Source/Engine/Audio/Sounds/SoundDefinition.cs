using System.ComponentModel.DataAnnotations;
using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>
/// A sound as gameplay plays it (a <c>sound</c> data asset): its files, one picked at random each play and never the same twice
/// running, how loud and at what pitch within a range, its bus, and whether it sounds from a place in the world.
/// </summary>
public sealed class SoundDefinition
{
    /// <summary>The extension of sound files; a <c>.fvdata</c> file of type <c>sound</c> reads the same.</summary>
    public const string Extension = ".fvsound";

    /// <summary>The variations, <c>.wav</c> or <c>.ogg</c> files in the content folder.</summary>
    [ContentFile(".wav", ".ogg")]
    public List<string> Files { get; set; } = [];

    public float Gain { get; set; } = 1;

    /// <summary>How far each play's gain may fall below <see cref="Gain"/>, as a share of it.</summary>
    [Range(0, 1)]
    public float GainVariation { get; set; }

    public float Pitch { get; set; } = 1;

    /// <summary>How far each play's pitch may lie above or below <see cref="Pitch"/>, as a share of it.</summary>
    [Range(0, 1)]
    public float PitchVariation { get; set; }

    public AudioBus Bus { get; set; } = AudioBus.Effects;

    /// <summary>Whether it is heard from where it is played, fading with distance and panned; otherwise it plays as it is.</summary>
    public bool Spatial { get; set; } = true;

    /// <summary>The distance in metres within which it is at full gain.</summary>
    public float MinDistance { get; set; } = 1;

    /// <summary>The distance in metres at which it is gone.</summary>
    public float MaxDistance { get; set; } = 20;

    public bool Loop { get; set; }

    /// <summary>The shortest time in seconds between two plays; a play sooner than that is dropped.</summary>
    public float Throttle { get; set; }
}
