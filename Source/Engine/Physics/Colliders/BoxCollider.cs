using System.Numerics;

using BepuPhysics;
using BepuPhysics.Collidables;

namespace FlickerVoid.Engine.Physics.Colliders;

/// <summary>A box centred on the entity, <see cref="Size"/> wide, high and long.</summary>
public sealed class BoxCollider : Collider
{
    public Vector3 Size { get; set; } = Vector3.One;

    internal override TypedIndex AddShape(Shapes shapes)
    {
        Box box = CreateShape();
        return shapes.Add(box);
    }

    internal override BodyInertia ComputeInertia(float mass)
    {
        Box box = CreateShape();
        return box.ComputeInertia(mass);
    }

    private Box CreateShape()
    {
        return new Box(Size.X, Size.Y, Size.Z);
    }
}
