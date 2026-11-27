using System.ComponentModel.DataAnnotations;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Audio.Music;

/// <summary>
/// One layer of a cue: a loop that sounds while the track is wanted, coming in on the cue's grid after an optional fill-in and
/// going out on it with an optional fill-out; or, with a <see cref="Scatter"/> sound instead, single sounds thrown around the
/// listener at random while it is wanted.
/// </summary>
public sealed class MusicTrack
{
    /// <summary>What music areas and scripts ask for it by.</summary>
    public string Name { get; set; } = string.Empty;

    [ContentFile(".wav", ".ogg")]
    public string Loop { get; set; } = string.Empty;

    /// <summary>The loop's length in bars; 0 takes it from the file's length, rounded to whole bars.</summary>
    [Range(0, 256)]
    public int Bars { get; set; }

    /// <summary>Played once on the grid before the loop comes in, the loop starting when its bars are over.</summary>
    [ContentFile(".wav", ".ogg")]
    public string FillIn { get; set; } = string.Empty;

    /// <summary>The tempo the track's files were exported at; when it differs from the cue's the files are stretched to the cue's without changing their pitch. 0 for the cue's own.</summary>
    [Range(0, 400)]
    public float Tempo { get; set; }

    /// <summary>Played once from the line the loop stops on.</summary>
    [ContentFile(".wav", ".ogg")]
    public string FillOut { get; set; } = string.Empty;

    public float Gain { get; set; } = 1;

    /// <summary>Whether the track sounds only while a music area or a script asks for it by name.</summary>
    public bool ByRequest { get; set; }

    /// <summary>What must hold for the track to sound, checked for the listener; empty always holds.</summary>
    public ConditionSet When { get; set; } = new();

    /// <summary>The line of the grid it comes in on.</summary>
    public MusicQuantize Start { get; set; } = MusicQuantize.Bar;

    /// <summary>The line of the grid it goes out on.</summary>
    public MusicQuantize Stop { get; set; } = MusicQuantize.Bar;

    public MusicPhase Phase { get; set; } = MusicPhase.Locked;

    /// <summary>The seconds the loop fades in over.</summary>
    [Range(0, 600)]
    public float FadeIn { get; set; }

    /// <summary>The seconds the loop fades out over, from the line it stops on.</summary>
    [Range(0, 600)]
    public float FadeOut { get; set; }

    /// <summary>A <c>sound</c> data asset played around the listener at random while the track is wanted, in place of a loop.</summary>
    [ContentFile(".fvsound", ".fvdata")]
    public string Scatter { get; set; } = string.Empty;

    /// <summary>The shortest wait in seconds between two scattered sounds.</summary>
    [Range(0, 3600)]
    public float EveryMin { get; set; } = 10;

    /// <summary>The longest wait in seconds between two scattered sounds.</summary>
    [Range(0, 3600)]
    public float EveryMax { get; set; } = 30;

    /// <summary>The nearest a scattered sound is thrown from the listener, in metres.</summary>
    public float NearDistance { get; set; } = 5;

    /// <summary>The farthest a scattered sound is thrown from the listener, in metres.</summary>
    public float FarDistance { get; set; } = 15;
}
