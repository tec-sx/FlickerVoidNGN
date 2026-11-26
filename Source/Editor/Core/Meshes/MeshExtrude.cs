using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>A solid a tool made and what the tool leaves selected in it: the faces, or the edges when it names none.</summary>
public sealed record MeshResult(SolidMesh Solid, IReadOnlyList<int> Faces)
{
    public IReadOnlyList<SolidEdge> Edges { get; init; } = [];
}

/// <summary>
/// Extrudes the selected faces as one region, as Blender's region extrude does: the region's faces move along their average normal
/// and a wall of new faces joins them to the faces they were cut from. Points the region has all round are left without a face and
/// go.
/// </summary>
public static class MeshExtrude
{
    /// <summary>
    /// The solid with the faces pushed out by the distance, negative to push in, and the pushed faces; null when no face is selected
    /// or all of them are, as there is then no wall to build.
    /// </summary>
    public static MeshResult? Faces(SolidMesh solid, IReadOnlySet<int> faces, float distance)
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

        List<Vector3> points = [.. solid.Points];
        Dictionary<int, int> copies = [];
        Vector3 push = GetNormal(solid, faces) * distance;
        List<int[]> result = [];
        List<int[]> walls = [];
        List<int> moved = [];
        for (int face = 0; face < solid.Faces.Count; face++)
        {
            IReadOnlyList<int> corners = solid.Faces[face];
            if (!faces.Contains(face))
            {
                result.Add([.. corners]);
                continue;
            }

            int[] pushed = new int[corners.Count];
            for (int corner = 0; corner < corners.Count; corner++)
            {
                pushed[corner] = GetCopy(solid, points, copies, push, corners[corner]);
            }

            moved.Add(result.Count);
            result.Add(pushed);
            for (int corner = 0; corner < corners.Count; corner++)
            {
                int from = corners[corner];
                int to = corners[(corner + 1) % corners.Count];
                int across = owner[(to, from)];
                if (!faces.Contains(across))
                {
                    walls.Add([from, to, copies[to], copies[from]]);
                }
            }
        }

        result.AddRange(walls);
        return Compact(points, result, moved);
    }

    private static int GetCopy(SolidMesh solid, List<Vector3> points, Dictionary<int, int> copies, Vector3 push, int point)
    {
        if (copies.TryGetValue(point, out int copy))
        {
            return copy;
        }

        copy = points.Count;
        points.Add(solid.Points[point] + push);
        copies[point] = copy;
        return copy;
    }

    private static Vector3 GetNormal(SolidMesh solid, IReadOnlySet<int> faces)
    {
        Vector3 sum = Vector3.Zero;
        foreach (int face in faces)
        {
            IReadOnlyList<int> corners = solid.Faces[face];
            for (int corner = 0; corner < corners.Count; corner++)
            {
                Vector3 current = solid.Points[corners[corner]];
                Vector3 next = solid.Points[corners[(corner + 1) % corners.Count]];
                sum += Vector3.Cross(current, next);
            }
        }

        if (sum.LengthSquared() < 1e-12f)
        {
            return Vector3.Zero;
        }

        return Vector3.Normalize(sum);
    }

    // Drops the points no face names any more and renumbers the rest.
    private static MeshResult Compact(List<Vector3> points, List<int[]> faces, List<int> moved)
    {
        int[] newNumber = new int[points.Count];
        Array.Fill(newNumber, -1);
        List<Vector3> kept = [];
        foreach (int[] face in faces)
        {
            foreach (int point in face)
            {
                if (newNumber[point] < 0)
                {
                    newNumber[point] = -2;
                }
            }
        }

        for (int point = 0; point < points.Count; point++)
        {
            if (newNumber[point] == -2)
            {
                newNumber[point] = kept.Count;
                kept.Add(points[point]);
            }
        }

        List<IReadOnlyList<int>> renumbered = [];
        foreach (int[] face in faces)
        {
            renumbered.Add([.. face.Select(point => newNumber[point])]);
        }

        return new MeshResult(new SolidMesh(kept, renumbered), moved);
    }
}
