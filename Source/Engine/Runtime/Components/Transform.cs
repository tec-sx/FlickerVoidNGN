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

    /// <summary>The matrix that scales, then rotates, then moves a point from the entity's space into its world.</summary>
    public readonly Matrix4x4 ToMatrix()
    {
        Matrix4x4 scale = Matrix4x4.CreateScale(Scale);
        Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(Rotation);
        Matrix4x4 translation = Matrix4x4.CreateTranslation(Position);
        return scale * rotation * translation;
    }
}
