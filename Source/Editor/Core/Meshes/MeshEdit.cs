using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>Edits that move the points of a solid and leave its faces as they are, so the solid stays closed and valid.</summary>
public static class MeshEdit
{
    /// <summary>The middle of the points, the pivot a rotation or a scale turns about; the origin for none.</summary>
    public static Vector3 GetCentre(SolidMesh solid, IReadOnlySet<int> points)
    {
        if (points.Count == 0)
        {
            return Vector3.Zero;
        }

        Vector3 sum = Vector3.Zero;
        foreach (int point in points)
        {
            sum += solid.Points[point];
        }

        return sum / points.Count;
    }

    public static SolidMesh Move(SolidMesh solid, IReadOnlySet<int> points, Vector3 offset)
    {
        Matrix4x4 move = Matrix4x4.CreateTranslation(offset);
        return Transform(solid, points, move);
    }

    /// <summary>Turns the points about their middle by the angles in degrees about X, Y and Z.</summary>
    public static SolidMesh Rotate(SolidMesh solid, IReadOnlySet<int> points, Vector3 degrees)
    {
        Vector3 centre = GetCentre(solid, points);
        float yaw = float.DegreesToRadians(degrees.Y);
        float pitch = float.DegreesToRadians(degrees.X);
        float roll = float.DegreesToRadians(degrees.Z);
        Quaternion turn = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
        Matrix4x4 about = Matrix4x4.CreateTranslation(-centre) * Matrix4x4.CreateFromQuaternion(turn) * Matrix4x4.CreateTranslation(centre);
        return Transform(solid, points, about);
    }

    /// <summary>Scales the points about their middle by a factor along each axis.</summary>
    public static SolidMesh Scale(SolidMesh solid, IReadOnlySet<int> points, Vector3 factor)
    {
        Vector3 centre = GetCentre(solid, points);
        Matrix4x4 about = Matrix4x4.CreateTranslation(-centre) * Matrix4x4.CreateScale(factor) * Matrix4x4.CreateTranslation(centre);
        return Transform(solid, points, about);
    }

    private static SolidMesh Transform(SolidMesh solid, IReadOnlySet<int> points, in Matrix4x4 matrix)
    {
        Vector3[] moved = [.. solid.Points];
        foreach (int point in points)
        {
            moved[point] = Vector3.Transform(solid.Points[point], matrix);
        }

        return new SolidMesh(moved, solid.Faces);
    }
}
