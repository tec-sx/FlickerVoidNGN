using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>
/// Mirrored editing about the part's centre line, its X = 0 plane: a point's twin is the point at the same height and depth on the
/// other side, a point on the plane is its own twin, and an edit on one side is carried over to the twins.
/// </summary>
public static class MeshMirror
{
    private const float Tolerance = 1e-4f;

    /// <summary>The twin of each point, or -1 for a point without one.</summary>
    public static int[] FindTwins(SolidMesh solid)
    {
        int[] twins = new int[solid.Points.Count];
        for (int point = 0; point < twins.Length; point++)
        {
            twins[point] = -1;
            Vector3 mirrored = Reflect(solid.Points[point]);
            for (int other = 0; other < twins.Length; other++)
            {
                if (Vector3.DistanceSquared(solid.Points[other], mirrored) < Tolerance * Tolerance)
                {
                    twins[point] = other;
                    break;
                }
            }
        }

        return twins;
    }

    /// <summary>The faces with the faces across the centre line from them, those that have one.</summary>
    public static HashSet<int> ExpandFaces(SolidMesh solid, IReadOnlySet<int> faces)
    {
        int[] twins = FindTwins(solid);
        Dictionary<string, int> byPoints = [];
        for (int face = 0; face < solid.Faces.Count; face++)
        {
            byPoints[GetKey(solid.Faces[face])] = face;
        }

        HashSet<int> expanded = [.. faces];
        foreach (int face in faces)
        {
            IEnumerable<int> across = solid.Faces[face].Select(point => twins[point]);
            if (across.Contains(-1))
            {
                continue;
            }

            if (byPoints.TryGetValue(GetKey([.. across]), out int twin))
            {
                expanded.Add(twin);
            }
        }

        return expanded;
    }

    /// <summary>The edges with the edges across the centre line from them, those that have one.</summary>
    public static HashSet<SolidEdge> ExpandEdges(SolidMesh solid, IReadOnlySet<SolidEdge> edges)
    {
        int[] twins = FindTwins(solid);
        SolidTopology topology = new(solid);
        HashSet<SolidEdge> all = [.. topology.Edges];
        HashSet<SolidEdge> expanded = [.. edges];
        foreach (SolidEdge edge in edges)
        {
            if (twins[edge.A] < 0 || twins[edge.B] < 0)
            {
                continue;
            }

            SolidEdge twin = SolidEdge.Of(twins[edge.A], twins[edge.B]);
            if (all.Contains(twin))
            {
                expanded.Add(twin);
            }
        }

        return expanded;
    }

    /// <summary>
    /// Carries what an edit did to the <paramref name="moved"/> points over to their twins that were not moved themselves, and keeps
    /// points on the centre line on it. Returns the edited solid when nothing is left to do.
    /// </summary>
    public static SolidMesh Follow(SolidMesh before, SolidMesh edited, IReadOnlySet<int> moved)
    {
        int[] twins = FindTwins(before);
        Vector3[] points = [.. edited.Points];
        foreach (int point in moved)
        {
            int twin = twins[point];
            if (twin == point)
            {
                points[point].X = 0;
            }
            else if (twin >= 0 && !moved.Contains(twin))
            {
                points[twin] = Reflect(points[point]);
            }
        }

        return new SolidMesh(points, edited.Faces);
    }

    private static Vector3 Reflect(Vector3 point) => new(-point.X, point.Y, point.Z);

    private static string GetKey(IReadOnlyList<int> points) => string.Join(',', points.Order());
}
