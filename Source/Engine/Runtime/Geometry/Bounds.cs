using System.Numerics;

namespace FlickerVoid.Engine.Runtime.Geometry;

/// <summary>An axis-aligned box from its smallest to its largest corner, such as what a mesh or a body takes up.</summary>
public readonly record struct Bounds(Vector3 Min, Vector3 Max)
{
    /// <summary>A box holding nothing, which grows to whatever it includes first.</summary>
    public static Bounds Empty => new(new Vector3(float.MaxValue), new Vector3(float.MinValue));

    public bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;

    public Vector3 Center => (Min + Max) / 2;

    /// <summary>Half the box's size along each axis.</summary>
    public Vector3 Extents => (Max - Min) / 2;

    public Bounds Include(Vector3 point) => new(Vector3.Min(Min, point), Vector3.Max(Max, point));

    public Bounds Include(Bounds other) => new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

    /// <summary>Whether the boxes overlap or touch; an empty box meets nothing.</summary>
    public bool Intersects(Bounds other)
    {
        if (IsEmpty || other.IsEmpty)
        {
            return false;
        }

        return Min.X <= other.Max.X && Max.X >= other.Min.X
            && Min.Y <= other.Max.Y && Max.Y >= other.Min.Y
            && Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
    }

    /// <summary>The box around this one after a matrix moves, turns and scales it, such as a model's bounds placed in the world.</summary>
    public Bounds Transform(in Matrix4x4 matrix)
    {
        if (IsEmpty)
        {
            return this;
        }

        Vector3 center = Vector3.Transform(Center, matrix);
        Vector3 extents = Extents;
        Vector3 right = new(matrix.M11, matrix.M12, matrix.M13);
        Vector3 up = new(matrix.M21, matrix.M22, matrix.M23);
        Vector3 forward = new(matrix.M31, matrix.M32, matrix.M33);
        Vector3 placed = (Vector3.Abs(right) * extents.X) + (Vector3.Abs(up) * extents.Y) + (Vector3.Abs(forward) * extents.Z);
        return new Bounds(center - placed, center + placed);
    }
}
