using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>Plays a sound on its entity while the entity is active, such as a lamp's hum or a street's ambience.</summary>
public struct SoundEmitter
{
    public SoundEmitter()
    {
    }

    /// <summary>The <c>sound</c> data asset to play, usually a looping one.</summary>
    [ContentFile(".fvdata")]
    public string Sound { get; set; } = string.Empty;
}
