namespace FlickerVoid.Engine.Audio.Music;

/// <summary>What a save keeps of the music: each bus's cues from the lowest up with the bars they had played, and the tracks scripts asked for.</summary>
public sealed class MusicSaveState
{
    public List<SavedCue> Cues { get; set; } = [];

    public List<string> Layers { get; set; } = [];
}
