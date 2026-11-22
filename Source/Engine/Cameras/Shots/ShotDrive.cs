namespace FlickerVoid.Engine.Cameras.Shots;

/// <summary>What moves a shot along its rail.</summary>
public enum ShotDrive
{
    /// <summary>The director plays its timeline, to a key or to the end, at the keys' times.</summary>
    Timed,

    /// <summary>Its target's nearest point on the rail sets its place on the timeline.</summary>
    Tracked,
}
