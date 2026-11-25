namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>What a mood value belongs to and is edited with.</summary>
public enum MoodOwner
{
    /// <summary>A place's light and air, which change over the hours of its day.</summary>
    Environment,

    /// <summary>The camera's lens and finish, which the main camera carries from place to place.</summary>
    Lens,
}
