using System.Numerics;
using Arch.Core;

namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>
/// What gameplay knows of sound: it plays <c>sound</c> data assets by their content path and stops them, never touching the
/// mixer, so another backend can stand behind it.
/// </summary>
public interface IAudioEngine
{
    /// <summary>Plays a sound as it is, wherever the listener stands; a spatial sound plays at the listener.</summary>
    SoundHandle Play(string sound);

    /// <summary>Plays a sound at a place in the world.</summary>
    SoundHandle Play(string sound, Vector3 position);

    /// <summary>Plays a sound on an entity, following it while it lives.</summary>
    SoundHandle Play(string sound, Entity entity);

    /// <summary>Fades a sound out over the seconds and ends it.</summary>
    void Stop(SoundHandle handle, float fadeSeconds = 0.05f);

    bool IsPlaying(SoundHandle handle);
}
