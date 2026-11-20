using FlickerVoid.Engine.Physics.Colliders;

namespace FlickerVoid.Engine.Physics;

/// <summary>A body that never moves, such as a floor or a wall; others collide with it.</summary>
public struct StaticBody
{
    public required Collider Collider { get; set; }
}
