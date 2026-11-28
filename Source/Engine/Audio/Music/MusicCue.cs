using System.ComponentModel.DataAnnotations;

using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Audio.Music;

/// <summary>
/// Music or ambience as layers (a <c>music</c> data asset): a tempo and meter that make its grid of beats, bars and phrases, and
/// the tracks that come and go on it. A tempo of 0 makes a cue without a grid, such as an ambience, whose tracks change at once
/// and fade. Pushed over another cue of its bus (the hub's song over the level's), it comes in after its fill-in and leaves with
/// its fill-out, the cue below holding its place meanwhile.
/// </summary>
public sealed class MusicCue
{
    /// <summary>The extension of music cue files; a <c>.fvdata</c> file of type <c>music</c> reads the same.</summary>
    public const string Extension = ".fvmusic";

    /// <summary>Beats a minute; 0 for no grid.</summary>
    [Range(0, 400)]
    public float Tempo { get; set; } = 120;

    /// <summary>Whether, pushed over another cue, it plays on that cue's tempo and meter instead of its own.</summary>
    public bool TempoFromBelow { get; set; }

    [Range(1, 16)]
    public int BeatsPerBar { get; set; } = 4;

    [Range(1, 64)]
    public int PhraseBars { get; set; } = 4;

    /// <summary>The bus its tracks play on, and the stack of cues it belongs to: one cue of a bus is heard at a time.</summary>
    public AudioBus Bus { get; set; } = AudioBus.Music;

    public float Gain { get; set; } = 1;

    public List<MusicTrack> Tracks { get; set; } = [];

    /// <summary>Points on the phrase that tell gameplay when the music passes them.</summary>
    public List<MusicNotify> Notifies { get; set; } = [];

    /// <summary>The parts of a song, one after the other from the cue's start.</summary>
    public List<MusicSection> Sections { get; set; } = [];

    /// <summary>Bars the cue can pick up from when it is taken back from under another: the first one at or after where it stopped, else the first.</summary>
    public List<MusicContinuePoint> ContinuePoints { get; set; } = [];

    /// <summary>Played once when the cue is pushed over another, which plays on under it; the cue starts when its bars are over.</summary>
    [ContentFile(".wav", ".ogg")]
    public string FillIn { get; set; } = string.Empty;

    /// <summary>Played once when the cue is taken off another, which continues when its bars are over.</summary>
    [ContentFile(".wav", ".ogg")]
    public string FillOut { get; set; } = string.Empty;

    /// <summary>A copy of the cue on another cue's tempo and meter.</summary>
    public MusicCue WithGridOf(MusicCue other)
    {
        MusicCue copy = (MusicCue)MemberwiseClone();
        copy.Tempo = other.Tempo;
        copy.BeatsPerBar = other.BeatsPerBar;
        return copy;
    }
}
