using System.Text.Json.Serialization;

using BepuPhysics;
using BepuPhysics.Collidables;

namespace FlickerVoid.Engine.Physics.Colliders;

/// <summary>The shape of a physics body; scene files pick one with "type".</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BoxCollider), "box")]
[JsonDerivedType(typeof(SphereCollider), "sphere")]
[JsonDerivedType(typeof(CapsuleCollider), "capsule")]
public abstract class Collider
{
    internal abstract TypedIndex AddShape(Shapes shapes);

    internal abstract BodyInertia ComputeInertia(float mass);
}
