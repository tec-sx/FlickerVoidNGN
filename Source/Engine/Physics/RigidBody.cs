using FlickerVoid.Engine.Physics.Colliders;

namespace FlickerVoid.Engine.Physics;

/// <summary>A body that physics moves: dynamic with a mass, or kinematic when only code moves it.</summary>
public struct RigidBody
{
    public RigidBody()
    {
    }

    public required Collider Collider { get; set; }

    public float Mass { get; set; } = 1;

    public bool Kinematic { get; set; }
}
