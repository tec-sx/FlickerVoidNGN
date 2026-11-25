using System.Numerics;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Solids;

/// <summary>
/// A closed solid made of points and flat faces, the shape greyboxed creations are made of: each face lists its points' numbers
/// counter-clockwise seen from outside, as Blender and glTF do, and every edge is shared by exactly two faces going opposite ways.
/// </summary>
[JsonConverter(typeof(SolidMeshJsonConverter))]
public sealed class SolidMesh
{
    private readonly Vector3[] _points;
    private readonly int[][] _faces;

    /// <summary>Makes a solid of the points and faces, refusing one that is open, has a face turned the wrong way or names a point it lacks.</summary>
    public SolidMesh(IReadOnlyList<Vector3> points, IReadOnlyList<IReadOnlyList<int>> faces)
    {
        _points = [.. points];
        _faces = new int[faces.Count][];
        for (int i = 0; i < faces.Count; i++)
        {
            _faces[i] = [.. faces[i]];
        }

        Validate();
    }

    public IReadOnlyList<Vector3> Points => _points;

    public IReadOnlyList<IReadOnlyList<int>> Faces => _faces;

    /// <summary>A box of <paramref name="size"/> standing on its origin, centred across, as a greybox stands.</summary>
    public static SolidMesh Box(Vector3 size)
    {
        Vector3 half = size / 2;
        Vector3[] points = new Vector3[8];
        for (int i = 0; i < points.Length; i++)
        {
            float x = (i & 1) == 0 ? -half.X : half.X;
            float y = (i & 2) == 0 ? 0 : size.Y;
            float z = (i & 4) == 0 ? -half.Z : half.Z;
            points[i] = new Vector3(x, y, z);
        }

        // Each face starts at its bottom left corner seen from outside, as the greybox's faces do, so the two draw alike.
        int[][] faces =
        [
            [5, 1, 3, 7],
            [0, 4, 6, 2],
            [6, 7, 3, 2],
            [0, 1, 5, 4],
            [4, 5, 7, 6],
            [1, 0, 2, 3],
        ];
        return new SolidMesh(points, faces);
    }

    /// <summary>A cylinder of <paramref name="sides"/> flat sides standing on its origin around the up axis, such as a wheel before it is turned on its side.</summary>
    public static SolidMesh Cylinder(float radius, float height, int sides)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 3);

        Vector3[] points = new Vector3[sides * 2];
        for (int i = 0; i < sides; i++)
        {
            float angle = MathF.Tau * i / sides;
            float x = radius * MathF.Cos(angle);
            float z = -radius * MathF.Sin(angle);
            points[i] = new Vector3(x, 0, z);
            points[sides + i] = new Vector3(x, height, z);
        }

        // Seen from above, the points go round counter-clockwise.
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

    /// <summary>The solid moved, turned, scaled or mirrored by <paramref name="matrix"/>; a mirror turns the faces round, so they still face outward.</summary>
    public SolidMesh Transform(Matrix4x4 matrix)
    {
        Vector3[] points = new Vector3[_points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = Vector3.Transform(_points[i], matrix);
        }

        bool mirrors = matrix.GetDeterminant() < 0;
        int[][] faces = new int[_faces.Length][];
        for (int i = 0; i < faces.Length; i++)
        {
            int[] face = [.. _faces[i]];
            if (mirrors)
            {
                Array.Reverse(face);
            }

            faces[i] = face;
        }

        return new SolidMesh(points, faces);
    }

    /// <summary>
    /// The faces as triangles wound clockwise seen from outside, as the engine draws, each face with its own flat normal and texture
    /// coordinates in metres starting at its top left corner: across runs level with the ground (along X on a level face), down runs down
    /// the face (towards -Z on a face looking up).
    /// </summary>
    public SolidTriangles Triangulate()
    {
        List<SolidVertex> vertices = [];
        List<uint> indices = [];
        foreach (int[] face in _faces)
        {
            Vector3 normal = GetNormal(face);
            Vector3 across = Vector3.Cross(Vector3.UnitY, normal);
            if (across.LengthSquared() < 1e-6f)
            {
                across = Vector3.UnitX;
            }

            across = Vector3.Normalize(across);
            Vector3 up = Vector3.Cross(normal, across);
            float left = float.MaxValue;
            float top = float.MinValue;
            foreach (int point in face)
            {
                left = MathF.Min(left, Vector3.Dot(_points[point], across));
                top = MathF.Max(top, Vector3.Dot(_points[point], up));
            }

            uint first = (uint)vertices.Count;
            foreach (int point in face)
            {
                Vector3 position = _points[point];
                Vector2 coordinates = new(Vector3.Dot(position, across) - left, top - Vector3.Dot(position, up));
                vertices.Add(new SolidVertex(position, normal, coordinates));
            }

            // A fan over the counter-clockwise corners, each triangle turned round to clockwise.
            for (uint corner = 1; corner + 1 < face.Length; corner++)
            {
                indices.AddRange([first, first + corner + 1, first + corner]);
            }
        }

        return new SolidTriangles([.. vertices], [.. indices]);
    }

    /// <summary>The face's outward unit normal by Newell's method, which holds for any flat polygon wound counter-clockwise from outside.</summary>
    private Vector3 GetNormal(int[] face)
    {
        Vector3 sum = Vector3.Zero;
        for (int i = 0; i < face.Length; i++)
        {
            Vector3 current = _points[face[i]];
            Vector3 next = _points[face[(i + 1) % face.Length]];
            sum.X += (current.Y - next.Y) * (current.Z + next.Z);
            sum.Y += (current.Z - next.Z) * (current.X + next.X);
            sum.Z += (current.X - next.X) * (current.Y + next.Y);
        }

        return Vector3.Normalize(sum);
    }

    private void Validate()
    {
        HashSet<(int From, int To)> edges = [];
        for (int i = 0; i < _faces.Length; i++)
        {
            int[] face = _faces[i];
            if (face.Length < 3)
            {
                throw new ArgumentException($"The solid's face {i} has {face.Length} points; a face needs at least three points.");
            }

            for (int corner = 0; corner < face.Length; corner++)
            {
                int point = face[corner];
                if (point < 0 || point >= _points.Length)
                {
                    throw new ArgumentException($"The solid's face {i} names point {point}, but the solid has {_points.Length} points.");
                }

                int next = face[(corner + 1) % face.Length];
                if (!edges.Add((point, next)))
                {
                    throw new ArgumentException($"The solid has a face turned the wrong way: two faces run from point {point} to point {next}.");
                }
            }
        }

        foreach ((int from, int to) in edges)
        {
            if (!edges.Contains((to, from)))
            {
                throw new ArgumentException($"The solid is open: only one face meets the edge from point {from} to point {to}.");
            }
        }
    }
}
