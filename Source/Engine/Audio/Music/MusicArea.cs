namespace FlickerVoid.Engine.Audio.Music;

/// <summary>Asks for tracks by name while a trigger activator stands in the entity's trigger area, such as the drums of a room.</summary>
public struct MusicArea
{
    public MusicArea()
    {
    }

    /// <summary>The names of the tracks, in whichever cue is playing.</summary>
    public List<string> Layers { get; set; } = [];
}
