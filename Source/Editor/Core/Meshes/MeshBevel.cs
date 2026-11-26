using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Meshes;

/// <summary>
/// Bevels edges as Blender's bevel does: each selected edge becomes a strip of faces <c>width</c> wide on each face, flat or
/// rounded, in N rows. At a point where several selected edges meet the strips are joined by a face; at a point where one meets,
/// which must have three edges, the strip ends on the two edges beside it. Other points, such as a strip ending at four edges, are
/// refused.
/// </summary>
public static class MeshBevel
{
    private const float MinimumMiter = 0.1f;
    private const float MinimumSine = 1e-3f;

    /// <summary>
    /// The solid with the edges beveled and the strips' faces; null when no edge is selected, a point takes a bevel the rules above
    /// refuse, or the width or the rows are not positive. <paramref name="round"/> curves the strips toward the old edge.
    /// </summary>
    public static MeshResult? Edges(SolidMesh solid, IReadOnlySet<SolidEdge> edges, float width, int rows, bool round)
    {
        if (edges.Count == 0 || width <= 0 || rows < 1)
        {
            return null;
        }

        SolidTopology topology = new(solid);
        Dictionary<int, Corner> corners = [];
        List<Vector3> points = [.. solid.Points];
        foreach (int point in edges.SelectMany(edge => new[] { edge.A, edge.B }).Distinct())
        {
            Corner? corner = MakeCorner(solid, topology, edges, point, width, points);
            if (corner is null)
            {
                return null;
            }

            corners[point] = corner;
        }

        Dictionary<(SolidEdge, int), int[]> profiles = [];
        foreach (SolidEdge edge in edges)
        {
            foreach (int end in new[] { edge.A, edge.B })
            {
                (int along, int back) = topology.GetFaces(edge);
                int first = corners[end].PointOfFace[along];
                int last = corners[end].PointOfFace[back];
                profiles[(edge, end)] = MakeProfile(solid, points, end, first, last, rows, round);
            }
        }

        List<IReadOnlyList<int>> faces = [];
        for (int face = 0; face < solid.Faces.Count; face++)
        {
            faces.Add(Rebuild(solid, face, corners, profiles));
        }

        List<int> strips = [];
        foreach (SolidEdge edge in edges)
        {
            int[] atA = profiles[(edge, edge.A)];
            int[] atB = profiles[(edge, edge.B)];
            for (int row = 0; row < rows; row++)
            {
                strips.Add(faces.Count);
                faces.Add([atB[row], atA[row], atA[row + 1], atB[row + 1]]);
            }
        }

        foreach ((int point, Corner corner) in corners)
        {
            if (corner.Selected.Count < 2)
            {
                continue;
            }

            IReadOnlyList<int>? cap = MakeCap(point, corner, profiles);
            if (cap is null)
            {
                return null;
            }

            if (cap.Count >= 3)
            {
                faces.Add(cap);
            }
        }

        return Compact(points, faces, strips);
    }

    private sealed class Corner
    {
        public required List<SolidEdge> Around { get; init; }

        public List<SolidEdge> Selected { get; } = [];

        public Dictionary<int, int> PointOfFace { get; } = [];

        /// <summary>For a point with one selected edge, the point put on each edge beside it, by the far end of that edge.</summary>
        public Dictionary<int, int> PointOnEdge { get; } = [];

        }

    private static Corner? MakeCorner(SolidMesh solid, SolidTopology topology, IReadOnlySet<SolidEdge> selected, int point, float width, List<Vector3> points)
    {
        IReadOnlyList<SolidEdge> around = topology.GetPointEdges(point);
        if (around.Count < 3)
        {
            return null;
        }

        Corner corner = new() { Around = [.. around] };
        int[] between = new int[around.Count];
        for (int index = 0; index < around.Count; index++)
        {
            int from = around[index].GetOther(point);
            int to = around[(index + 1) % around.Count].GetOther(point);
            between[index] = FindFace(solid, point, from, to);
            if (between[index] < 0)
            {
                return null;
            }
        }

        List<int> picks = [];
        for (int index = 0; index < around.Count; index++)
        {
            if (selected.Contains(around[index]))
            {
                picks.Add(index);
                corner.Selected.Add(around[index]);
            }
        }

        if (picks.Count == 1)
        {
            if (around.Count != 3)
            {
                return null;
            }

            PlaceEnd(solid, point, around, picks[0], between, width, points, corner);
            return corner;
        }

        PlaceSectors(solid, point, around, picks, between, width, points, corner);
        return corner;
    }

    private static int FindFace(SolidMesh solid, int point, int from, int to)
    {
        for (int face = 0; face < solid.Faces.Count; face++)
        {
            IReadOnlyList<int> corners = solid.Faces[face];
            int at = corners.ToList().IndexOf(point);
            if (at < 0)
            {
                continue;
            }

            int previous = corners[(at + corners.Count - 1) % corners.Count];
            int next = corners[(at + 1) % corners.Count];
            if ((previous == from && next == to) || (previous == to && next == from))
            {
                return face;
            }
        }

        return -1;
    }

    // A strip ends beside three edges: its two sides meet the edges next to it, and the face between them is cut across.
    private static void PlaceEnd(SolidMesh solid, int point, IReadOnlyList<SolidEdge> around, int pick, int[] between, float width, List<Vector3> points, Corner corner)
    {
        SolidEdge strip = around[pick];
        SolidEdge next = around[(pick + 1) % 3];
        SolidEdge previous = around[(pick + 2) % 3];
        int nextFace = between[pick];
        int previousFace = between[(pick + 2) % 3];
        int onNext = AddOnEdge(solid, point, strip, next, width, points);
        int onPrevious = AddOnEdge(solid, point, strip, previous, width, points);
        corner.PointOfFace[nextFace] = onNext;
        corner.PointOfFace[previousFace] = onPrevious;
        corner.PointOnEdge[next.GetOther(point)] = onNext;
        corner.PointOnEdge[previous.GetOther(point)] = onPrevious;
    }

    private static int AddOnEdge(SolidMesh solid, int point, SolidEdge strip, SolidEdge beside, float width, List<Vector3> points)
    {
        Vector3 origin = solid.Points[point];
        Vector3 along = solid.Points[strip.GetOther(point)] - origin;
        Vector3 toward = solid.Points[beside.GetOther(point)] - origin;
        float length = toward.Length();
        Vector3 across = Vector3.Cross(Vector3.Normalize(along), Vector3.Normalize(toward));
        float sine = MathF.Max(across.Length(), MinimumSine);
        float distance = MathF.Min(width / sine, length * 0.99f);
        points.Add(origin + (toward / length * distance));
        return points.Count - 1;
    }

    // Each run of faces between two selected edges gets one point, where the edges' offsets meet.
    private static void PlaceSectors(SolidMesh solid, int point, IReadOnlyList<SolidEdge> around, List<int> picks, int[] between, float width, List<Vector3> points, Corner corner)
    {
        for (int sector = 0; sector < picks.Count; sector++)
        {
            int first = picks[sector];
            int after = picks[(sector + 1) % picks.Count];
            int last = (after + around.Count - 1) % around.Count;
            Vector3 start = GetInward(solid, point, around[first], between[first]);
            Vector3 end = GetInward(solid, point, around[after], between[last]);
            float miter = MathF.Max(1 + Vector3.Dot(start, end), MinimumMiter);
            points.Add(solid.Points[point] + ((start + end) / miter * width));
            int added = points.Count - 1;
            int index = first;
            while (true)
            {
                corner.PointOfFace[between[index]] = added;
                if (index == last)
                {
                    break;
                }

                index = (index + 1) % around.Count;
            }
        }
    }

    // The direction within the face, square to the edge, toward the inside of the face.
    private static Vector3 GetInward(SolidMesh solid, int point, SolidEdge edge, int face)
    {
        IReadOnlyList<int> corners = solid.Faces[face];
        Vector3 sum = Vector3.Zero;
        for (int corner = 0; corner < corners.Count; corner++)
        {
            Vector3 current = solid.Points[corners[corner]];
            Vector3 next = solid.Points[corners[(corner + 1) % corners.Count]];
            sum += Vector3.Cross(current, next);
        }

        int at = corners.ToList().IndexOf(point);
        int far = edge.GetOther(point);
        Vector3 along = solid.Points[far] - solid.Points[point];
        if (corners[(at + 1) % corners.Count] != far)
        {
            along = -along;
        }

        Vector3 across = Vector3.Cross(sum, along);
        if (across.LengthSquared() < 1e-12f)
        {
            return Vector3.Zero;
        }

        return Vector3.Normalize(across);
    }

    private static int[] MakeProfile(SolidMesh solid, List<Vector3> points, int end, int first, int last, int rows, bool round)
    {
        int[] profile = new int[rows + 1];
        profile[0] = first;
        profile[rows] = last;
        Vector3 start = points[first];
        Vector3 finish = points[last];
        Vector3 control = solid.Points[end];
        for (int row = 1; row < rows; row++)
        {
            float t = (float)row / rows;
            Vector3 position = Vector3.Lerp(start, finish, t);
            if (round)
            {
                Vector3 near = Vector3.Lerp(start, control, t);
                Vector3 far = Vector3.Lerp(control, finish, t);
                position = Vector3.Lerp(near, far, t);
            }

            points.Add(position);
            profile[row] = points.Count - 1;
        }

        return profile;
    }

    private static List<int> Rebuild(SolidMesh solid, int face, Dictionary<int, Corner> corners, Dictionary<(SolidEdge, int), int[]> profiles)
    {
        IReadOnlyList<int> old = solid.Faces[face];
        List<int> cycle = [];
        for (int index = 0; index < old.Count; index++)
        {
            int point = old[index];
            if (!corners.TryGetValue(point, out Corner? corner))
            {
                cycle.Add(point);
                continue;
            }

            if (corner.PointOfFace.TryGetValue(face, out int replaced))
            {
                cycle.Add(replaced);
                continue;
            }

            int previous = old[(index + old.Count - 1) % old.Count];
            cycle.AddRange(GetChain(point, corner, previous, profiles));
        }

        return cycle;
    }

    // The face between the two edges beside a strip's end is cut across: it takes the strip's profile in place of the point,
    // starting from the side point on the edge it comes from.
    private static int[] GetChain(int point, Corner corner, int previous, Dictionary<(SolidEdge, int), int[]> profiles)
    {
        int[] profile = profiles[(corner.Selected[0], point)];
        int start = corner.PointOnEdge[previous];
        if (start == profile[0])
        {
            return profile;
        }

        return [.. profile.Reverse()];
    }

    private static IReadOnlyList<int>? MakeCap(int point, Corner corner, Dictionary<(SolidEdge, int), int[]> profiles)
    {
        List<int[]> runs = [];
        foreach (SolidEdge edge in corner.Selected)
        {
            int[] profile = profiles[(edge, point)];
            if (edge.A == point)
            {
                runs.Add([.. profile.Reverse()]);
            }
            else
            {
                runs.Add(profile);
            }
        }

        List<int> cap = [.. runs[0]];
        runs.RemoveAt(0);
        while (runs.Count > 0)
        {
            int tail = cap[^1];
            int next = runs.FindIndex(run => run[0] == tail);
            if (next < 0)
            {
                break;
            }

            cap.AddRange(runs[next].Skip(1));
            runs.RemoveAt(next);
        }

        if (runs.Count > 0 || cap[^1] != cap[0])
        {
            return null;
        }

        cap.RemoveAt(cap.Count - 1);
        return cap;
    }

    private static MeshResult Compact(List<Vector3> points, List<IReadOnlyList<int>> faces, List<int> strips)
    {
        int[] newNumber = new int[points.Count];
        Array.Fill(newNumber, -1);
        foreach (IReadOnlyList<int> face in faces)
        {
            foreach (int point in face)
            {
                newNumber[point] = 0;
            }
        }

        List<Vector3> kept = [];
        for (int point = 0; point < points.Count; point++)
        {
            if (newNumber[point] == 0)
            {
                newNumber[point] = kept.Count;
                kept.Add(points[point]);
            }
        }

        List<IReadOnlyList<int>> renumbered = [];
        foreach (IReadOnlyList<int> face in faces)
        {
            renumbered.Add([.. face.Select(point => newNumber[point])]);
        }

        return new MeshResult(new SolidMesh(kept, renumbered), strips);
    }
}
