namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>The overrides a mood director lays over the environment, lowest first.</summary>
public enum MoodLayer
{
    /// <summary>The mood the scene names.</summary>
    Scene,

    /// <summary>The mood the story wants now.</summary>
    Story,

    /// <summary>The mood of the shot on screen.</summary>
    Shot,

    /// <summary>What a tool forces over everything, such as a viewer's options.</summary>
    Debug,
}
