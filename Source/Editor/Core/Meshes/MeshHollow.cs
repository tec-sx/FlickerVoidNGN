using System.Numerics;

using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>
/// Makes a solid hollow: a copy of it, every face moved in by the wall's thickness, is taken out of it with Manifold, so a shell is
/// left. Faces chosen to be open are moved out instead, so the hollow breaks through them: a box with its top open is a tray.
/// </summary>
public static class MeshHollow
{
    /// <summary>How far an open face's inner copy stands out of it, so the opening leaves no sliver.</summary>
    private const float Overshoot = 0.05f;

    /// <summary>Keeps the solve steady where a point's faces lie in fewer than three planes.</summary>
    private const float Steady = 1e-4f;

    /// <summary>
    /// The solid made a shell of the thickness, open through <paramref name="open"/>; null for a thickness not above zero, or when
    /// Manifold is missing or finds nothing left (a wall thicker than the solid).
    /// </summary>
    public static MeshResult? Shell(SolidMesh solid, float thickness, IReadOnlySet<int> open)
    {
        if (thickness <= 0 || !SolidBoolean.IsAvailable)
        {
            return null;
        }

        SolidMesh inner = Offset(solid, thickness, open);
        float volume = GetVolume(inner);
        if (volume <= 0)
        {
            return null;
        }

        SolidMesh? shell = SolidBoolean.Combine(solid, inner, BooleanOperation.Subtract);
        if (shell is null || shell.Faces.Count <= solid.Faces.Count)
        {
            return null;
        }

        return new MeshResult(shell, []);
    }

    /// <summary>
    /// The solid with every face's plane moved in by the distance (out by a little for an open face): each point goes where the
    /// planes of its faces meet again, solved by least squares, so a box's corners stay square.
    /// </summary>
    public static SolidMesh Offset(SolidMesh solid, float distance, IReadOnlySet<int> open)
    {
        int count = solid.Points.Count;
        Vector3[] normals = new Vector3[solid.Faces.Count];
        for (int face = 0; face < normals.Length; face++)
        {
            normals[face] = GetNormal(solid, face);
        }

        // For each point, the sums of n nᵀ and of n times how far its plane moves (negative: inward).
        Matrix4x4[] sums = new Matrix4x4[count];
        Vector3[] pulls = new Vector3[count];
        for (int face = 0; face < normals.Length; face++)
        {
            Vector3 normal = normals[face];
            float shift = -distance;
            if (open.Contains(face))
            {
                shift = Overshoot;
            }

            foreach (int point in solid.Faces[face])
            {
                sums[point] = Add(sums[point], normal);
                pulls[point] += normal * shift;
            }
        }

        Vector3[] points = new Vector3[count];
        for (int point = 0; point < count; point++)
        {
            Matrix4x4 system = sums[point];
            system.M11 += Steady;
            system.M22 += Steady;
            system.M33 += Steady;
            system.M44 = 1;
            Vector3 move = Vector3.Zero;
            if (Matrix4x4.Invert(system, out Matrix4x4 inverse))
            {
                move = Vector3.TransformNormal(pulls[point], inverse);
            }

            points[point] = solid.Points[point] + move;
        }

        return new SolidMesh(points, solid.Faces);
    }

    // A wall thicker than half the solid turns the inner copy inside out, so its volume goes negative.
    private static float GetVolume(SolidMesh solid)
    {
        float sum = 0;
        foreach (IReadOnlyList<int> corners in solid.Faces)
        {
            Vector3 first = solid.Points[corners[0]];
            for (int corner = 1; corner + 1 < corners.Count; corner++)
            {
                Vector3 b = solid.Points[corners[corner]];
                Vector3 c = solid.Points[corners[corner + 1]];
                sum += Vector3.Dot(first, Vector3.Cross(b, c));
            }
        }

        return sum / 6;
    }

    private static Matrix4x4 Add(Matrix4x4 sum, Vector3 normal)
    {
        sum.M11 += normal.X * normal.X;
        sum.M12 += normal.X * normal.Y;
        sum.M13 += normal.X * normal.Z;
        sum.M21 += normal.Y * normal.X;
        sum.M22 += normal.Y * normal.Y;
        sum.M23 += normal.Y * normal.Z;
        sum.M31 += normal.Z * normal.X;
        sum.M32 += normal.Z * normal.Y;
        sum.M33 += normal.Z * normal.Z;
        return sum;
    }

    private static Vector3 GetNormal(SolidMesh solid, int face)
    {
        IReadOnlyList<int> corners = solid.Faces[face];
        Vector3 sum = Vector3.Zero;
        for (int corner = 0; corner < corners.Count; corner++)
        {
            Vector3 current = solid.Points[corners[corner]];
            Vector3 next = solid.Points[corners[(corner + 1) % corners.Count]];
            sum += Vector3.Cross(current, next);
        }

        return Vector3.Normalize(sum);
    }
}
