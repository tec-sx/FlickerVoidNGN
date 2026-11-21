using System.Numerics;

namespace FlickerVoid.Gameplay.World.Objects.Doors;

/// <summary>A swinging door while the game plays: its closed rotation, how far open it is and wants to be, and the rattle left.</summary>
public struct DoorHinge
{
    public Quaternion Closed { get; set; }

    /// <summary>Degrees open now.</summary>
    public float Angle { get; set; }

    /// <summary>Degrees open it swings to.</summary>
    public float Target { get; set; }

    /// <summary>Seconds of rattle left.</summary>
    public float Rattle { get; set; }
}
