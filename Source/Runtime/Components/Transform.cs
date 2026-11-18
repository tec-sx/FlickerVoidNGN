using System.Numerics;

namespace FlickerVoid.Engine.Runtime.Components;

/// <summary>Where an entity is in its world: position, rotation and scale; a new transform has no rotation and a scale of one.</summary>
public struct Transform
{
    public Transform()
    {
    }

    public Vector3 Position { get; set; }

    public Quaternion Rotation { get; set; } = Quaternion.Identity;

    public Vector3 Scale { get; set; } = Vector3.One;
}
