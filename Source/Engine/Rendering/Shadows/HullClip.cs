using System.Numerics;

using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>
/// The corners of where a hexahedron, such as a view cut at the shadow distance, overlaps a box: each shape's corners inside the
/// other and where each shape's edges cross the other's faces.
/// </summary>
internal static class HullClip
{
    /// <summary>The most points <see cref="Clip"/> gives: both shapes' corners, and every edge of each through every face of the other.</summary>
    public const int MaxPoints = 8 + 8 + (2 * 12 * 6);

    private const float Tolerance = 1e-4f;

    // Corners are numbered by their axes' bits: x is bit 0, y bit 1 and z bit 2, as a box's and a view's.
    private static readonly int[][] Faces = [[0, 2, 4], [1, 5, 3], [0, 4, 1], [2, 3, 6], [0, 1, 2], [4, 6, 5]];
    private static readonly (int From, int To)[] Edges =
    [
        (0, 1), (2, 3), (4, 5), (6, 7),
        (0, 2), (1, 3), (4, 6), (5, 7),
        (0, 4), (1, 5), (2, 6), (3, 7),
    ];

    /// <summary>Writes the overlap's corners into <paramref name="points"/> (at least <see cref="MaxPoints"/> long); gives how many, none where they don't meet.</summary>
    public static int Clip(ReadOnlySpan<Vector3> corners, in Bounds box, Span<Vector3> points)
    {
        Span<Vector3> boxCorners = stackalloc Vector3[8];
        for (int index = 0; index < boxCorners.Length; index++)
        {
            float x = (index & 1) == 0 ? box.Min.X : box.Max.X;
            float y = (index & 2) == 0 ? box.Min.Y : box.Max.Y;
            float z = (index & 4) == 0 ? box.Min.Z : box.Max.Z;
            boxCorners[index] = new Vector3(x, y, z);
        }

        Span<Vector4> hullPlanes = stackalloc Vector4[6];
        Span<Vector4> boxPlanes = stackalloc Vector4[6];
        FindPlanes(corners, hullPlanes);
        FindPlanes(boxCorners, boxPlanes);
        int count = AddInside(corners, hullPlanes, boxPlanes, points, 0);
        return AddInside(boxCorners, boxPlanes, hullPlanes, points, count);
    }

    /// <summary>Adds the shape's corners inside the other and where its edges cross the other's faces inside it.</summary>
    private static int AddInside(ReadOnlySpan<Vector3> corners, ReadOnlySpan<Vector4> ownPlanes, ReadOnlySpan<Vector4> otherPlanes, Span<Vector3> points, int count)
    {
        foreach (Vector3 corner in corners)
        {
            if (IsInside(corner, otherPlanes))
            {
                points[count++] = corner;
            }
        }

        foreach ((int from, int to) in Edges)
        {
            Vector3 start = corners[from];
            Vector3 end = corners[to];
            foreach (Vector4 plane in otherPlanes)
            {
                float startSide = Side(plane, start);
                float endSide = Side(plane, end);
                if ((startSide < 0) == (endSide < 0))
                {
                    continue;
                }

                float along = startSide / (startSide - endSide);
                Vector3 crossing = Vector3.Lerp(start, end, along);
                if (IsInside(crossing, otherPlanes) && IsInside(crossing, ownPlanes))
                {
                    points[count++] = crossing;
                }
            }
        }

        return count;
    }

    /// <summary>The planes of the six faces, facing in.</summary>
    private static void FindPlanes(ReadOnlySpan<Vector3> corners, Span<Vector4> planes)
    {
        Vector3 center = Vector3.Zero;
        foreach (Vector3 corner in corners)
        {
            center += corner;
        }

        center /= corners.Length;
        for (int face = 0; face < Faces.Length; face++)
        {
            int[] indices = Faces[face];
            Vector3 first = corners[indices[0]];
            Vector3 toSecond = corners[indices[1]] - first;
            Vector3 toThird = corners[indices[2]] - first;
            Vector3 cross = Vector3.Cross(toSecond, toThird);
            Vector3 normal = Vector3.Normalize(cross);
            Vector4 plane = new(normal, -Vector3.Dot(normal, first));
            if (Side(plane, center) < 0)
            {
                plane = -plane;
            }

            planes[face] = plane;
        }
    }

    private static bool IsInside(Vector3 point, ReadOnlySpan<Vector4> planes)
    {
        foreach (Vector4 plane in planes)
        {
            if (Side(plane, point) < -Tolerance * MathF.Max(1, MathF.Abs(plane.W)))
            {
                return false;
            }
        }

        return true;
    }

    private static float Side(Vector4 plane, Vector3 point)
    {
        return Vector3.Dot(new Vector3(plane.X, plane.Y, plane.Z), point) + plane.W;
    }
}
