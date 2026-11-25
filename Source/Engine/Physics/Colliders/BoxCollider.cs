using System.Numerics;

using Jolt = JoltPhysicsSharp;

namespace FlickerVoid.Engine.Physics.Colliders;

/// <summary>A box centred on the entity, <see cref="Size"/> wide, high and long.</summary>
public sealed class BoxCollider : Collider
{
    /// <summary>The radius Jolt rounds a box's edges by for collision; rays still meet its flat faces.</summary>
    private const float ConvexRadius = 0.05f;

    public Vector3 Size { get; set; } = Vector3.One;

    internal override Jolt.Shape CreateShape()
    {
        Vector3 halfExtent = Size / 2;
        float smallest = MathF.Min(halfExtent.X, MathF.Min(halfExtent.Y, halfExtent.Z));
        float radius = MathF.Min(ConvexRadius, smallest);
        return new Jolt.BoxShape(halfExtent, radius);
    }
}
