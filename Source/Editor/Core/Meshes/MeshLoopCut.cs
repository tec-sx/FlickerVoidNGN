using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>
/// Cuts loops across the ring of quads through an edge, as Blender's loop cut does: every edge of the ring gets a new point at each
/// cut and each quad of the ring is split along them, so the solid stays closed. A ring that ends at a face that is not a quad
/// leaves that face with the extra points on its edge.
/// </summary>
public static class MeshLoopCut
{
    private const float Limit = 0.01f;

    /// <summary>
    /// Adds <paramref name="cuts"/> loops, evenly spaced between the two ends of the ring's edges and all moved by
    /// <paramref name="slide"/> (-1 to 1, a fraction of the space between loops) toward either end; the new edges are selected.
    /// Null when there are no cuts, the edge isn't the solid's or the ring crosses no quad.
    /// </summary>
    public static MeshResult? Cut(SolidMesh solid, SolidEdge edge, int cuts, float slide)
    {
        SolidTopology topology = new(solid);
        if (cuts < 1 || !topology.Edges.Contains(edge))
        {
            return null;
        }

        IReadOnlyList<int> loop = topology.GetFaceLoop(edge);
        if (loop.Count == 0)
        {
            return null;
        }

        IReadOnlyList<SolidEdge> ring = topology.GetEdgeRing(edge);
        Dictionary<SolidEdge, int> starts = GetStarts(solid, edge, ring, loop);
        List<Vector3> points = [.. solid.Points];
        Dictionary<SolidEdge, int[]> made = [];
        foreach (SolidEdge ringEdge in ring)
        {
            int start = starts[ringEdge];
            int end = ringEdge.GetOther(start);
            int[] along = new int[cuts];
            for (int cut = 0; cut < cuts; cut++)
            {
                float fraction = Math.Clamp(((cut + 1 + slide) / (cuts + 1)), Limit, 1 - Limit);
                along[cut] = points.Count;
                points.Add(Vector3.Lerp(solid.Points[start], solid.Points[end], fraction));
            }

            made[ringEdge] = along;
        }

        HashSet<int> split = [.. loop];
        List<IReadOnlyList<int>> faces = [];
        List<SolidEdge> added = [];
        for (int face = 0; face < solid.Faces.Count; face++)
        {
            List<int> cycle = GetCycle(solid.Faces[face], made, starts);
            if (!split.Contains(face))
            {
                faces.Add(cycle);
                continue;
            }

            faces.AddRange(Split(cycle, made, cuts, added));
        }

        return new MeshResult(new SolidMesh(points, faces), []) { Edges = added };
    }

    // The end of each ring edge the cuts are counted from: the one beside the previous edge's start, quad by quad.
    private static Dictionary<SolidEdge, int> GetStarts(SolidMesh solid, SolidEdge first, IReadOnlyList<SolidEdge> ring, IReadOnlyList<int> loop)
    {
        Dictionary<SolidEdge, int> starts = new() { [first] = first.A };
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (int face in loop)
            {
                IReadOnlyList<int> corners = solid.Faces[face];
                foreach (SolidEdge known in ring.Where(starts.ContainsKey))
                {
                    int start = starts[known];
                    int at = IndexOf(corners, start);
                    if (at < 0 || IndexOf(corners, known.GetOther(start)) < 0)
                    {
                        continue;
                    }

                    int next = corners[(at + 1) % corners.Count];
                    int previous = corners[(at + corners.Count - 1) % corners.Count];
                    int beside = next;
                    if (next == known.GetOther(start))
                    {
                        beside = previous;
                    }

                    foreach (SolidEdge other in ring)
                    {
                        if (!starts.ContainsKey(other) && other.A != other.B && (other.A == beside || other.B == beside) && IndexOf(corners, other.A) >= 0 && IndexOf(corners, other.B) >= 0)
                        {
                            starts[other] = beside;
                            grew = true;
                        }
                    }
                }
            }
        }

        return starts;
    }

    private static int IndexOf(IReadOnlyList<int> corners, int point)
    {
        for (int index = 0; index < corners.Count; index++)
        {
            if (corners[index] == point)
            {
                return index;
            }
        }

        return -1;
    }

    // The face's corners with the cut points of each ring edge it has put in along it.
    private static List<int> GetCycle(IReadOnlyList<int> corners, Dictionary<SolidEdge, int[]> made, Dictionary<SolidEdge, int> starts)
    {
        List<int> cycle = [];
        for (int corner = 0; corner < corners.Count; corner++)
        {
            int from = corners[corner];
            int to = corners[(corner + 1) % corners.Count];
            cycle.Add(from);
            SolidEdge edge = SolidEdge.Of(from, to);
            if (!made.TryGetValue(edge, out int[]? along))
            {
                continue;
            }

            if (starts[edge] == from)
            {
                cycle.AddRange(along);
            }
            else
            {
                cycle.AddRange(along.Reverse());
            }
        }

        return cycle;
    }

    // A quad of the ring has the cut points of two edges: split along each pair at the same cut.
    private static List<IReadOnlyList<int>> Split(List<int> cycle, Dictionary<SolidEdge, int[]> made, int cuts, List<SolidEdge> added)
    {
        List<List<int>> pieces = [cycle];
        List<int[]> lines = [.. made.Values];
        for (int cut = 0; cut < cuts; cut++)
        {
            int[] inFace = [.. lines.Select(along => along[cut]).Where(point => cycle.Contains(point))];
            if (inFace.Length != 2)
            {
                continue;
            }

            int first = inFace[0];
            int second = inFace[1];
            int holder = pieces.FindIndex(piece => piece.Contains(first) && piece.Contains(second));
            List<int> piece = pieces[holder];
            pieces.RemoveAt(holder);
            int from = piece.IndexOf(first);
            int to = piece.IndexOf(second);
            pieces.Add(Walk(piece, from, to));
            pieces.Add(Walk(piece, to, from));
            added.Add(SolidEdge.Of(first, second));
        }

        return [.. pieces];
    }

    private static List<int> Walk(List<int> cycle, int from, int to)
    {
        List<int> walked = [cycle[from]];
        int at = from;
        while (at != to)
        {
            at = (at + 1) % cycle.Count;
            walked.Add(cycle[at]);
        }

        return walked;
    }
}
