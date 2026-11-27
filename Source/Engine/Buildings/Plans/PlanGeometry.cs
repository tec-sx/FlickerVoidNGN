using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>
/// The shapes a storey's plan makes, without Booleans: each wall's centre line and solid (mitred where two walls meet), the rooms the
/// walls close, the outline the floor is laid under, and where along a wall an opening stands. Points are across the floor: X, and Z
/// as Y.
/// </summary>
public static class PlanGeometry
{
    /// <summary>How far a mitre's corner may stand from the wall's corner, in wall thicknesses, before the end is cut square.</summary>
    private const float MitreLimit = 4;

    private const float Tiny = 1e-4f;

    public static Vector2 GetPoint(PlanCorner corner) => new(corner.X, corner.Z);

    public static PlanCorner? FindCorner(BuildingStorey storey, int id) => storey.Corners.Find(corner => corner.Id == id);

    public static PlanWall? FindWall(BuildingStorey storey, int id) => storey.Walls.Find(wall => wall.Id == id);

    public static float GetThickness(BuildingStorey storey, PlanWall wall) => wall.Thickness ?? storey.Wall;

    /// <summary>How high above the floor the wall's top stands.</summary>
    public static float GetHeight(BuildingStorey storey, PlanWall wall)
    {
        float full = storey.Height - storey.Floor;
        switch (wall.Height)
        {
            case WallHeight.Half:
                return full / 2;
            case WallHeight.Custom:
                return Math.Clamp(wall.CustomHeight, 0.1f, full);
            default:
                return full;
        }
    }

    /// <summary>The wall's centre line from its start corner to its end corner: two points, or an arc's points.</summary>
    public static List<Vector2> GetPath(BuildingStorey storey, PlanWall wall)
    {
        PlanCorner? from = FindCorner(storey, wall.From);
        PlanCorner? to = FindCorner(storey, wall.To);
        if (from is null || to is null)
        {
            return [];
        }

        return GetPath(GetPoint(from), GetPoint(to), wall.Bulge, wall.Segments);
    }

    /// <summary>The centre line from <paramref name="start"/> to <paramref name="end"/>, bent so its middle stands <paramref name="bulge"/> to the left.</summary>
    public static List<Vector2> GetPath(Vector2 start, Vector2 end, float bulge, int segments)
    {
        Vector2 chord = end - start;
        float length = chord.Length();
        if (MathF.Abs(bulge) < Tiny || length < Tiny)
        {
            return [start, end];
        }

        // The arc through both corners whose middle stands the bulge off the chord's middle.
        float half = length / 2;
        float radius = ((half * half) + (bulge * bulge)) / (2 * MathF.Abs(bulge));
        Vector2 middle = (start + end) / 2;
        Vector2 left = Polygons.Left(chord);
        float side = MathF.Sign(bulge);
        Vector2 centre = middle - (left * side * (radius - MathF.Abs(bulge)));
        float startAngle = MathF.Atan2(start.Y - centre.Y, start.X - centre.X);
        float endAngle = MathF.Atan2(end.Y - centre.Y, end.X - centre.X);
        Vector2 top = middle + (left * bulge);
        float topAngle = MathF.Atan2(top.Y - centre.Y, top.X - centre.X);
        float sweep = Wrap(endAngle - startAngle);
        float toTop = Wrap(topAngle - startAngle);
        if (MathF.Sign(sweep) != MathF.Sign(toTop) || MathF.Abs(toTop) > MathF.Abs(sweep))
        {
            sweep -= MathF.Sign(sweep) * MathF.Tau;
        }

        int count = Math.Max(2, segments);
        List<Vector2> points = new(count + 1) { start };
        for (int step = 1; step < count; step++)
        {
            float angle = startAngle + (sweep * step / count);
            points.Add(centre + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius));
        }

        points.Add(end);
        return points;
    }

    private static float Wrap(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= MathF.Tau;
        }

        while (angle < -MathF.PI)
        {
            angle += MathF.Tau;
        }

        return angle;
    }

    public static float GetLength(IReadOnlyList<Vector2> path)
    {
        float length = 0;
        for (int index = 1; index < path.Count; index++)
        {
            length += Vector2.Distance(path[index - 1], path[index]);
        }

        return length;
    }

    /// <summary>The point the distance along the path, and the way the path runs there; the ends are held to the path.</summary>
    public static (Vector2 Point, Vector2 Direction) GetAlong(IReadOnlyList<Vector2> path, float distance)
    {
        if (path.Count < 2)
        {
            return (path.Count == 1 ? path[0] : Vector2.Zero, Vector2.UnitX);
        }

        float left = Math.Max(distance, 0);
        for (int index = 1; index < path.Count; index++)
        {
            Vector2 piece = path[index] - path[index - 1];
            float length = piece.Length();
            if (left <= length || index == path.Count - 1)
            {
                Vector2 direction = length > Tiny ? piece / length : Vector2.UnitX;
                float clamped = Math.Min(left, length);
                return (path[index - 1] + (direction * clamped), direction);
            }

            left -= length;
        }

        return (path[^1], Vector2.UnitX);
    }

    /// <summary>The distance along the path of the point on it nearest to <paramref name="point"/>, and how far that is.</summary>
    public static (float Along, float Distance) Project(IReadOnlyList<Vector2> path, Vector2 point)
    {
        float best = float.MaxValue;
        float along = 0;
        float walked = 0;
        for (int index = 1; index < path.Count; index++)
        {
            Vector2 a = path[index - 1];
            Vector2 piece = path[index] - a;
            float length = piece.Length();
            float t = 0;
            if (length > Tiny)
            {
                t = Math.Clamp(Vector2.Dot(point - a, piece) / (length * length), 0, 1);
            }

            Vector2 nearest = a + (piece * t);
            float distance = Vector2.Distance(nearest, point);
            if (distance < best)
            {
                best = distance;
                along = walked + (t * length);
            }

            walked += length;
        }

        return (along, best);
    }

    /// <summary>The walls that start or end at the corner.</summary>
    public static List<PlanWall> GetWallsAt(BuildingStorey storey, int corner) => storey.Walls.FindAll(wall => wall.From == corner || wall.To == corner);

    /// <summary>The way a wall's centre line leaves the corner, when it starts or ends there.</summary>
    public static Vector2 GetLeaving(BuildingStorey storey, PlanWall wall, int corner)
    {
        List<Vector2> path = GetPath(storey, wall);
        if (path.Count < 2)
        {
            return Vector2.UnitX;
        }

        if (wall.From == corner)
        {
            return Vector2.Normalize(path[1] - path[0]);
        }

        return Vector2.Normalize(path[^2] - path[^1]);
    }

    /// <summary>
    /// The wall's solid from <paramref name="bottom"/> to <paramref name="top"/> in the building's space: its centre line widened to its
    /// thickness, mitred against the one other wall at a corner where two walls meet (or two outside walls among more), and cut square
    /// elsewhere. Null for a wall without corners.
    /// </summary>
    public static SolidMesh? CreateWall(BuildingStorey storey, PlanWall wall, float bottom, float top)
    {
        List<Vector2> path = GetPath(storey, wall);
        if (path.Count < 2 || GetLength(path) < Tiny)
        {
            return null;
        }

        float thickness = GetThickness(storey, wall);
        (List<Vector2> left, List<Vector2> right) = GetSides(storey, wall, path, thickness);
        return Ribbon(left, right, bottom, top);
    }

    /// <summary>The wall's two faces across the floor: its left side and its right side, from start to end.</summary>
    public static (List<Vector2> Left, List<Vector2> Right) GetSides(BuildingStorey storey, PlanWall wall, IReadOnlyList<Vector2> path, float thickness)
    {
        float half = thickness / 2;
        int count = path.Count;
        List<Vector2> left = new(count);
        List<Vector2> right = new(count);
        for (int index = 0; index < count; index++)
        {
            if (index == 0 || index == count - 1)
            {
                left.Add(Vector2.Zero);
                right.Add(Vector2.Zero);
                continue;
            }

            Vector2 before = path[index] - path[index - 1];
            Vector2 after = path[index + 1] - path[index];
            left.Add(JoinSides(path[index], before, after, half, half));
            right.Add(JoinSides(path[index], before, after, -half, -half));
        }

        (Vector2 startLeft, Vector2 startRight) = GetEnd(storey, wall, wall.From, path[1] - path[0], half);
        left[0] = startLeft;
        right[0] = startRight;
        (Vector2 endLeft, Vector2 endRight) = GetEnd(storey, wall, wall.To, path[^2] - path[^1], half);

        // Seen from the end corner the wall runs the other way, so its left there is the wall's right.
        left[^1] = endRight;
        right[^1] = endLeft;
        return (left, right);
    }

    private static Vector2 JoinSides(Vector2 point, Vector2 before, Vector2 after, float beforeOffset, float afterOffset)
    {
        Vector2 first = point + (Polygons.Left(before) * beforeOffset);
        Vector2 second = point + (Polygons.Left(after) * afterOffset);
        Vector2? met = Polygons.Meet(first, before, second, after);
        if (met is not { } found || Vector2.Distance(found, point) > MitreLimit * MathF.Abs(beforeOffset) * 2)
        {
            return second;
        }

        return found;
    }

    /// <summary>The wall's left and right side at the corner, seen walking away from it along <paramref name="leaving"/>.</summary>
    private static (Vector2 Left, Vector2 Right) GetEnd(BuildingStorey storey, PlanWall wall, int cornerId, Vector2 leaving, float half)
    {
        PlanCorner corner = FindCorner(storey, cornerId)!;
        Vector2 point = GetPoint(corner);
        Vector2 side = Polygons.Left(leaving);
        Vector2 squareLeft = point + (side * half);
        Vector2 squareRight = point - (side * half);
        PlanWall? partner = FindPartner(storey, wall, cornerId);
        if (partner is null)
        {
            return (squareLeft, squareRight);
        }

        Vector2 other = GetLeaving(storey, partner, cornerId);
        float otherHalf = GetThickness(storey, partner) / 2;
        if (MathF.Abs(Polygons.Cross(Vector2.Normalize(leaving), other)) < 0.02f)
        {
            return (squareLeft, squareRight);
        }

        // Coming in along the partner and leaving along this wall, the partner's right side runs into this wall's left side.
        Vector2 otherSide = Polygons.Left(other);
        Vector2? left = Polygons.Meet(squareLeft, leaving, point - (otherSide * otherHalf), other);
        Vector2? right = Polygons.Meet(squareRight, leaving, point + (otherSide * otherHalf), other);
        float limit = MitreLimit * MathF.Max(half, otherHalf) * 2;
        if (left is not { } mitreLeft || right is not { } mitreRight || Vector2.Distance(mitreLeft, point) > limit || Vector2.Distance(mitreRight, point) > limit)
        {
            return (squareLeft, squareRight);
        }

        return (mitreLeft, mitreRight);
    }

    /// <summary>The wall this one is mitred against at the corner: the only other wall there, or the only other outside wall for an outside one.</summary>
    public static PlanWall? FindPartner(BuildingStorey storey, PlanWall wall, int corner)
    {
        List<PlanWall> others = GetWallsAt(storey, corner);
        others.Remove(wall);
        if (others.Count == 1)
        {
            return others[0];
        }

        if (wall.Outside)
        {
            List<PlanWall> outside = others.FindAll(static other => other.Outside);
            if (outside.Count == 1)
            {
                return outside[0];
            }
        }

        return null;
    }

    /// <summary>A closed solid between two side lines of equal length from <paramref name="bottom"/> to <paramref name="top"/>: a wall's prism.</summary>
    public static SolidMesh Ribbon(IReadOnlyList<Vector2> left, IReadOnlyList<Vector2> right, float bottom, float top)
    {
        int count = left.Count;
        List<Vector3> points = new(count * 4);

        // Per station: right bottom, left bottom, right top, left top.
        for (int index = 0; index < count; index++)
        {
            points.Add(new Vector3(right[index].X, bottom, right[index].Y));
            points.Add(new Vector3(left[index].X, bottom, left[index].Y));
            points.Add(new Vector3(right[index].X, top, right[index].Y));
            points.Add(new Vector3(left[index].X, top, left[index].Y));
        }

        List<int[]> faces = [];
        for (int index = 0; index + 1 < count; index++)
        {
            int rb = index * 4;
            int lb = rb + 1;
            int rt = rb + 2;
            int lt = rb + 3;
            int nrb = rb + 4;
            int nlb = rb + 5;
            int nrt = rb + 6;
            int nlt = rb + 7;
            faces.Add([rt, nrt, nlt, lt]);
            faces.Add([lb, nlb, nrb, rb]);
            faces.Add([rb, nrb, nrt, rt]);
            faces.Add([nlb, lb, lt, nlt]);
        }

        faces.Add([1, 0, 2, 3]);
        int last = (count - 1) * 4;
        faces.Add([last, last + 1, last + 3, last + 2]);
        return new SolidMesh(points, faces);
    }

    /// <summary>
    /// The outline the floor is laid under: the outside walls' loop widened to their outer faces, running round to the left; without
    /// a closed loop, the box round every corner and its walls. Empty without corners.
    /// </summary>
    public static List<Vector2> GetOutline(BuildingStorey storey)
    {
        List<Vector2>? loop = FindOutsideLoop(storey, out List<float> halves);
        if (loop is not null)
        {
            return Polygons.Offset(loop, halves);
        }

        if (storey.Corners.Count == 0)
        {
            return [];
        }

        float pad = storey.Wall / 2;
        float minX = storey.Corners.Min(static corner => corner.X) - pad;
        float maxX = storey.Corners.Max(static corner => corner.X) + pad;
        float minZ = storey.Corners.Min(static corner => corner.Z) - pad;
        float maxZ = storey.Corners.Max(static corner => corner.Z) + pad;
        return Polygons.ToLeft([new(minX, minZ), new(maxX, minZ), new(maxX, maxZ), new(minX, maxZ)]);
    }

    /// <summary>The outline of the floor's inside: the outside walls' loop narrowed to their inner faces; the outline itself without a loop.</summary>
    public static List<Vector2> GetInsideOutline(BuildingStorey storey)
    {
        List<Vector2>? loop = FindOutsideLoop(storey, out List<float> halves);
        if (loop is null)
        {
            return GetOutline(storey);
        }

        List<float> inward = [.. halves.Select(static half => -half)];
        return Polygons.Offset(loop, inward);
    }

    /// <summary>
    /// The biggest space the outside walls close, as their centre lines run round it to the left, with each edge's half thickness;
    /// null when they close none.
    /// </summary>
    public static List<Vector2>? FindOutsideLoop(BuildingStorey storey, out List<float> halves)
    {
        List<PlanWall> outside = storey.Walls.FindAll(static wall => wall.Outside);
        List<Face> faces = FindFaces(storey, outside);
        halves = [];
        Face? biggest = null;
        foreach (Face face in faces)
        {
            if (biggest is null || Polygons.Area(face.Points) > Polygons.Area(biggest.Points))
            {
                biggest = face;
            }
        }

        if (biggest is null)
        {
            return null;
        }

        halves = [.. biggest.Halves];
        return [.. biggest.Points];
    }

    /// <summary>
    /// The rooms the storey's walls close, each with the room record whose point is inside it (or -1), the polygon of its walls'
    /// centre lines running round to the left, and its floor's area inside the walls.
    /// </summary>
    public static List<RoomShape> FindRooms(BuildingStorey storey)
    {
        List<Face> faces = FindFaces(storey, storey.Walls);
        List<RoomShape> rooms = [];
        HashSet<int> used = [];
        foreach (Face face in faces)
        {
            int record = -1;
            for (int index = 0; index < storey.Rooms.Count; index++)
            {
                PlanRoom room = storey.Rooms[index];
                if (!used.Contains(index) && Polygons.Contains(face.Points, new Vector2(room.X, room.Z)))
                {
                    record = index;
                    used.Add(index);
                    break;
                }
            }

            float area = Polygons.Area(face.Points);
            for (int edge = 0; edge < face.Points.Count; edge++)
            {
                float length = Vector2.Distance(face.Points[edge], face.Points[(edge + 1) % face.Points.Count]);
                area -= length * face.Halves[edge];
            }

            rooms.Add(new RoomShape(record, face.Points, MathF.Max(area, 0)));
        }

        return rooms;
    }

    /// <summary>
    /// The bounded spaces the walls' centre lines close, each running round to the left with the half thickness of the wall along each
    /// edge: every wall is walked both ways, turning at each corner onto the next wall round, so each space is walked once.
    /// </summary>
    private static List<Face> FindFaces(BuildingStorey storey, IReadOnlyList<PlanWall> walls)
    {
        List<HalfEdge> edges = [];
        foreach (PlanWall wall in walls)
        {
            List<Vector2> path = GetPath(storey, wall);
            if (path.Count < 2 || wall.From == wall.To)
            {
                continue;
            }

            float half = GetThickness(storey, wall) / 2;
            List<Vector2> back = [.. path];
            back.Reverse();
            edges.Add(new HalfEdge(wall.From, wall.To, path, half));
            edges.Add(new HalfEdge(wall.To, wall.From, back, half));
        }

        Dictionary<int, List<HalfEdge>> leaving = [];
        foreach (HalfEdge edge in edges)
        {
            if (!leaving.TryGetValue(edge.From, out List<HalfEdge>? list))
            {
                list = [];
                leaving[edge.From] = list;
            }

            list.Add(edge);
        }

        foreach (List<HalfEdge> list in leaving.Values)
        {
            list.Sort(static (first, second) => first.Angle.CompareTo(second.Angle));
        }

        HashSet<HalfEdge> walked = [];
        List<Face> faces = [];
        foreach (HalfEdge start in edges)
        {
            if (walked.Contains(start))
            {
                continue;
            }

            List<Vector2> points = [];
            List<float> halves = [];
            HalfEdge current = start;
            int guard = edges.Count + 1;
            while (guard-- > 0 && walked.Add(current))
            {
                for (int index = 0; index < current.Path.Count - 1; index++)
                {
                    points.Add(current.Path[index]);
                    halves.Add(current.Half);
                }

                current = Next(leaving, current);
                if (current == start)
                {
                    break;
                }
            }

            if (points.Count >= 3 && Polygons.SignedArea(points) < -Tiny)
            {
                faces.Add(new Face(points, halves));
            }
        }

        return faces;
    }

    /// <summary>The edge leaving the end of <paramref name="edge"/> that comes next round, counter-clockwise from the way back.</summary>
    private static HalfEdge Next(Dictionary<int, List<HalfEdge>> leaving, HalfEdge edge)
    {
        List<HalfEdge> list = leaving[edge.To];
        Vector2 backward = edge.Path[^2] - edge.Path[^1];
        float back = MathF.Atan2(backward.Y, backward.X);
        foreach (HalfEdge candidate in list)
        {
            if (candidate.Angle > back + Tiny)
            {
                return candidate;
            }
        }

        return list[0];
    }

    /// <summary>One way along a wall between two corners, with the angle its first piece leaves at.</summary>
    private sealed class HalfEdge(int from, int to, List<Vector2> path, float half)
    {
        public int From { get; } = from;

        public int To { get; } = to;

        public List<Vector2> Path { get; } = path;

        public float Half { get; } = half;

        public float Angle { get; } = MathF.Atan2(path[1].Y - path[0].Y, path[1].X - path[0].X);
    }

    private sealed record Face(List<Vector2> Points, List<float> Halves);
}
