namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>The groups voices are mixed in, each with its own volume and filter, all under <see cref="Master"/>.</summary>
public enum AudioBus
{
    Master,
    Music,
    Ambience,
    Effects,
    Dialogue,
    Interface,
}
