using FlickerVoid.Engine.Physics.Colliders;

namespace FlickerVoid.Engine.Physics;

/// <summary>A body that stays put, such as a floor, a wall or a crate; others collide with it, and it follows its transform if moved.</summary>
public struct StaticBody
{
    public StaticBody()
    {
    }

    public required Collider Collider { get; set; }

    /// <summary>Whether paths go around it; small things that are picked up turn it off, so taking them builds no paths.</summary>
    public bool Obstacle { get; set; } = true;
}
