using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>
/// Insets the selected faces as one region, as Blender's inset does: the region's border moves inward within its faces by a
/// thickness, and a ring of new faces fills the gap to the faces round it. Points inside the region stay where they are.
/// </summary>
public static class MeshInset
{
    private const float MinimumMiter = 0.1f;

    /// <summary>
    /// The solid with the faces' border pulled in by the thickness, and the inner faces; null when no face is selected or all are,
    /// as there is then no border. A thickness larger than the faces allow turns them inside out, which is left to the user to see.
    /// </summary>
    public static MeshResult? Faces(SolidMesh solid, IReadOnlySet<int> faces, float thickness)
    {
        if (faces.Count == 0 || faces.Count == solid.Faces.Count)
        {
            return null;
        }

        Dictionary<(int, int), int> owner = [];
        for (int face = 0; face < solid.Faces.Count; face++)
        {
            IReadOnlyList<int> corners = solid.Faces[face];
            for (int corner = 0; corner < corners.Count; corner++)
            {
                int next = corners[(corner + 1) % corners.Count];
                owner[(corners[corner], next)] = face;
            }
        }

        List<(int From, int To, int Face)> border = [];
        foreach (int face in faces)
        {
            IReadOnlyList<int> corners = solid.Faces[face];
            for (int corner = 0; corner < corners.Count; corner++)
            {
                int from = corners[corner];
                int to = corners[(corner + 1) % corners.Count];
                if (!faces.Contains(owner[(to, from)]))
                {
                    border.Add((from, to, face));
                }
            }
        }

        List<Vector3> points = [.. solid.Points];
        Dictionary<int, int> inner = GetInnerPoints(solid, border, thickness, points);
        List<IReadOnlyList<int>> result = [];
        List<int> insetFaces = [];
        for (int face = 0; face < solid.Faces.Count; face++)
        {
            IReadOnlyList<int> corners = solid.Faces[face];
            if (!faces.Contains(face))
            {
                result.Add(corners);
                continue;
            }

            insetFaces.Add(result.Count);
            result.Add([.. corners.Select(point => inner.GetValueOrDefault(point, point))]);
        }

        foreach ((int from, int to, int _) in border)
        {
            result.Add([from, to, inner[to], inner[from]]);
        }

        return new MeshResult(new SolidMesh(points, result), insetFaces);
    }

    private static Dictionary<int, int> GetInnerPoints(SolidMesh solid, List<(int From, int To, int Face)> border, float thickness, List<Vector3> points)
    {
        Dictionary<int, List<Vector3>> inward = [];
        foreach ((int from, int to, int face) in border)
        {
            Vector3 normal = GetNormal(solid, face);
            Vector3 along = solid.Points[to] - solid.Points[from];
            Vector3 across = Vector3.Cross(normal, along);
            if (across.LengthSquared() < 1e-12f)
            {
                continue;
            }

            Vector3 unit = Vector3.Normalize(across);
            AddInward(inward, from, unit);
            AddInward(inward, to, unit);
        }

        Dictionary<int, int> inner = [];
        foreach ((int point, List<Vector3> directions) in inward)
        {
            inner[point] = points.Count;
            points.Add(solid.Points[point] + (GetOffset(directions) * thickness));
        }

        return inner;
    }

    private static void AddInward(Dictionary<int, List<Vector3>> inward, int point, Vector3 direction)
    {
        if (!inward.TryGetValue(point, out List<Vector3>? directions))
        {
            directions = [];
            inward[point] = directions;
        }

        directions.Add(direction);
    }

    // Two border edges meet at a corner: the offset that keeps both edges the thickness in from where they were.
    private static Vector3 GetOffset(List<Vector3> directions)
    {
        if (directions.Count == 2)
        {
            float miter = MathF.Max(1 + Vector3.Dot(directions[0], directions[1]), MinimumMiter);
            return (directions[0] + directions[1]) / miter;
        }

        Vector3 sum = Vector3.Zero;
        foreach (Vector3 direction in directions)
        {
            sum += direction;
        }

        if (sum.LengthSquared() < 1e-12f)
        {
            return Vector3.Zero;
        }

        return Vector3.Normalize(sum);
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

        if (sum.LengthSquared() < 1e-12f)
        {
            return Vector3.Zero;
        }

        return Vector3.Normalize(sum);
    }
}
