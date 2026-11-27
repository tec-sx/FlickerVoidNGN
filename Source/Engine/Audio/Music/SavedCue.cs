using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Music;

/// <summary>A cue of a bus's stack and the whole bars it had played.</summary>
public sealed class SavedCue
{
    public string Cue { get; set; } = string.Empty;

    public AudioBus Bus { get; set; }

    public int Bars { get; set; }
}
