namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>Names a playing sound, to stop it or ask after it; <see cref="None"/> is a sound that never started.</summary>
public readonly record struct SoundHandle(int Id)
{
    public static SoundHandle None => default;

    public bool IsNone => Id == 0;
}
