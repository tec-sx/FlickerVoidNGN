using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Foliage;

/// <summary>The greyboxes foliage is grown of: tapered trunks and limbs, and rounded crowns.</summary>
public static class FoliageShapes
{
    private static readonly float Golden = (1 + MathF.Sqrt(5)) / 2;

    /// <summary>The corners of an icosahedron, then its faces wound counter-clockwise seen from outside.</summary>
    private static readonly Vector3[] BlobCorners =
    [
        new(-1, Golden, 0), new(1, Golden, 0), new(-1, -Golden, 0), new(1, -Golden, 0),
        new(0, -1, Golden), new(0, 1, Golden), new(0, -1, -Golden), new(0, 1, -Golden),
        new(Golden, 0, -1), new(Golden, 0, 1), new(-Golden, 0, -1), new(-Golden, 0, 1),
    ];

    private static readonly int[][] BlobFaces =
    [
        [0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11],
        [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6], [7, 1, 8],
        [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9],
        [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1],
    ];

    /// <summary>A trunk or limb standing on its origin along the up axis, <paramref name="footRadius"/> wide at the bottom and <paramref name="topRadius"/> at the top.</summary>
    public static SolidMesh CreateTaper(float footRadius, float topRadius, float length, int sides)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 3);

        Vector3[] points = new Vector3[sides * 2];
        for (int i = 0; i < sides; i++)
        {
            float angle = MathF.Tau * i / sides;
            float x = MathF.Cos(angle);
            float z = -MathF.Sin(angle);
            points[i] = new Vector3(x * footRadius, 0, z * footRadius);
            points[sides + i] = new Vector3(x * topRadius, length, z * topRadius);
        }

        int[][] faces = new int[sides + 2][];
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            faces[i] = [i, next, sides + next, sides + i];
        }

        faces[sides] = [.. Enumerable.Range(sides, sides)];
        faces[sides + 1] = [.. Enumerable.Range(0, sides).Reverse()];
        return new SolidMesh(points, faces);
    }

    /// <summary>A rounded mass of leaves about its origin, reaching <paramref name="radii"/> along each axis: twenty flat faces.</summary>
    public static SolidMesh CreateBlob(Vector3 radii)
    {
        float reach = MathF.Sqrt(1 + (Golden * Golden));
        Vector3[] points = [.. BlobCorners.Select(corner => corner / reach * radii)];
        return new SolidMesh(points, BlobFaces);
    }
}
