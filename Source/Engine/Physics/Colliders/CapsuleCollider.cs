using Jolt = JoltPhysicsSharp;

namespace FlickerVoid.Engine.Physics.Colliders;

/// <summary>A capsule standing on the entity's up axis: a cylinder <see cref="Length"/> long with half spheres of <see cref="Radius"/> on both ends.</summary>
public sealed class CapsuleCollider : Collider
{
    public float Radius { get; set; } = 0.5f;

    public float Length { get; set; } = 1;

    internal override Jolt.Shape CreateShape()
    {
        return new Jolt.CapsuleShape(Length / 2, Radius);
    }
}
