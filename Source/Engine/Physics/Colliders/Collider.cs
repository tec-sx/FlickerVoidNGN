using System.Numerics;
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
    /// <summary>Where the shape's centre sits in the entity's space, such as half a door's width from its hinge.</summary>
    public Vector3 Offset { get; set; }

    /// <summary>The pose of the shape for an entity at <paramref name="position"/> turned by <paramref name="rotation"/>.</summary>
    internal RigidPose GetPose(Vector3 position, Quaternion rotation)
    {
        Vector3 offset = Vector3.Transform(Offset, rotation);
        return new RigidPose(position + offset, rotation);
    }

    /// <summary>The entity's position for a shape posed at <paramref name="pose"/>.</summary>
    internal Vector3 GetEntityPosition(in RigidPose pose)
    {
        Vector3 offset = Vector3.Transform(Offset, pose.Orientation);
        return pose.Position - offset;
    }

    internal abstract TypedIndex AddShape(Shapes shapes);

    internal abstract BodyInertia ComputeInertia(float mass);
}
