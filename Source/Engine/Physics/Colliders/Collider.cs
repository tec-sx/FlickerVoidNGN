using System.Numerics;
using System.Text.Json.Serialization;

using Jolt = JoltPhysicsSharp;

namespace FlickerVoid.Engine.Physics.Colliders;

/// <summary>The shape of a physics body; scene files pick one with "type".</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BoxCollider), "box")]
[JsonDerivedType(typeof(SphereCollider), "sphere")]
[JsonDerivedType(typeof(CapsuleCollider), "capsule")]
[JsonDerivedType(typeof(HullCollider), "hull")]
[JsonDerivedType(typeof(TriangleCollider), "triangles")]
public abstract class Collider
{
    /// <summary>Where the shape's centre sits in the entity's space, such as half a door's width from its hinge.</summary>
    public Vector3 Offset { get; set; }

    /// <summary>Where the shape's centre is for an entity at <paramref name="position"/> turned by <paramref name="rotation"/>.</summary>
    internal Vector3 GetBodyPosition(Vector3 position, Quaternion rotation)
    {
        Vector3 offset = Vector3.Transform(Offset, rotation);
        return position + offset;
    }

    /// <summary>The entity's position for a shape centred at <paramref name="bodyPosition"/> and turned by <paramref name="rotation"/>.</summary>
    internal Vector3 GetEntityPosition(Vector3 bodyPosition, Quaternion rotation)
    {
        Vector3 offset = Vector3.Transform(Offset, rotation);
        return bodyPosition - offset;
    }

    /// <summary>
    /// A new Jolt shape centred on the body; the caller disposes it once a body holds it. Throws <see cref="InvalidOperationException"/>
    /// saying what is wrong when the collider makes no shape.
    /// </summary>
    internal abstract Jolt.Shape CreateShape();
}
