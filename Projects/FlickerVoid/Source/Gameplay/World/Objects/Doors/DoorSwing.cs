namespace FlickerVoid.Gameplay.World.Objects.Doors;

/// <summary>A door that swings about its entity's origin, the hinge, when its toggle opens and closes, and rattles when a lock blocks it.</summary>
public struct DoorSwing
{
    public DoorSwing()
    {
    }

    /// <summary>Degrees about the up axis it swings open; negative swings the other way.</summary>
    public float OpenAngle { get; set; } = 90;

    /// <summary>Degrees per second.</summary>
    public float Speed { get; set; } = 120;
}
