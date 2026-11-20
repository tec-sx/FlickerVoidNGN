using BepuPhysics;
using BepuPhysics.Collidables;

namespace FlickerVoid.Engine.Physics.Colliders;

/// <summary>A sphere centred on the entity.</summary>
public sealed class SphereCollider : Collider
{
    public float Radius { get; set; } = 0.5f;

    internal override TypedIndex AddShape(Shapes shapes)
    {
        Sphere sphere = new(Radius);
        return shapes.Add(sphere);
    }

    internal override BodyInertia ComputeInertia(float mass)
    {
        Sphere sphere = new(Radius);
        return sphere.ComputeInertia(mass);
    }
}
