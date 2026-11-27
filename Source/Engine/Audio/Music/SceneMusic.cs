using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Audio.Music;

/// <summary>The cues a scene plays from its start: its music and its ambience; a scene without one stops both.</summary>
public struct SceneMusic
{
    public SceneMusic()
    {
    }

    /// <summary>A <c>music</c> data asset on the music bus.</summary>
    [ContentFile(".fvdata")]
    public string Music { get; set; } = string.Empty;

    /// <summary>A <c>music</c> data asset on the ambience bus.</summary>
    [ContentFile(".fvdata")]
    public string Ambience { get; set; } = string.Empty;
}
