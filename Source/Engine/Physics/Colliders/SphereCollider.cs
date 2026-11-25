using Jolt = JoltPhysicsSharp;

namespace FlickerVoid.Engine.Physics.Colliders;

/// <summary>A sphere centred on the entity.</summary>
public sealed class SphereCollider : Collider
{
    public float Radius { get; set; } = 0.5f;

    internal override Jolt.Shape CreateShape()
    {
        return new Jolt.SphereShape(Radius);
    }
}
