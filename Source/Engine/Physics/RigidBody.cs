using FlickerVoid.Engine.Physics.Colliders;

namespace FlickerVoid.Engine.Physics;

/// <summary>
/// A body that moves: dynamic, moved by physics with a mass, or kinematic, following its entity's transform as code moves it (a door,
/// a character), pushing dynamic bodies but never pushed.
/// </summary>
public struct RigidBody
{
    public RigidBody()
    {
    }

    public required Collider Collider { get; set; }

    public float Mass { get; set; } = 1;

    public bool Kinematic { get; set; }

    /// <summary>Whether paths go around it, built again once it comes to rest, as for a door; characters are not obstacles.</summary>
    public bool Obstacle { get; set; }
}
