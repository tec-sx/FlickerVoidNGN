using System.Numerics;

using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;

namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>
/// The building creator's edits of a storey's plan: pushing a wall along its normal, moving a corner, splitting a wall, bending it into
/// an arc, drawing walls (which join the corners and walls they meet and cross) and rectangular rooms, taking walls and corners out,
/// and keeping the rooms named as the walls close and open them. Openings stay at their distance along the wall they are cut through.
/// </summary>
public static class PlanEdits
{
    /// <summary>How near a point must come to a corner or a wall to join it, in metres.</summary>
    public const float Join = 0.15f;

    private const float Tiny = 1e-4f;

    /// <summary>The corner within <paramref name="radius"/> of the point, nearest first; null when there is none.</summary>
    public static PlanCorner? FindCorner(BuildingStorey storey, Vector2 point, float radius)
    {
        PlanCorner? best = null;
        float nearest = radius;
        foreach (PlanCorner corner in storey.Corners)
        {
            float distance = Vector2.Distance(PlanGeometry.GetPoint(corner), point);
            if (distance <= nearest)
            {
                nearest = distance;
                best = corner;
            }
        }

        return best;
    }

    /// <summary>The wall whose centre line passes within <paramref name="radius"/> of the point, with the distance along it; null for none.</summary>
    public static (PlanWall Wall, float Along)? FindWall(BuildingStorey storey, Vector2 point, float radius)
    {
        (PlanWall, float)? best = null;
        float nearest = radius;
        foreach (PlanWall wall in storey.Walls)
        {
            List<Vector2> path = PlanGeometry.GetPath(storey, wall);
            if (path.Count < 2)
            {
                continue;
            }

            float reach = Math.Max(radius, PlanGeometry.GetThickness(storey, wall) / 2);
            (float along, float distance) = PlanGeometry.Project(path, point);
            if (distance <= Math.Max(nearest, 0) && distance <= reach)
            {
                nearest = distance;
                best = (wall, along);
            }
        }

        return best;
    }

    /// <summary>The way out of the wall's right side at its middle (outside, for an outside wall walked round to the left).</summary>
    public static Vector2 GetNormal(BuildingStorey storey, PlanWall wall)
    {
        List<Vector2> path = PlanGeometry.GetPath(storey, wall);
        if (path.Count < 2)
        {
            return Vector2.UnitY;
        }

        Vector2 chord = path[^1] - path[0];
        if (chord.LengthSquared() < Tiny)
        {
            return Vector2.UnitY;
        }

        return -Polygons.Left(chord);
    }

    /// <summary>
    /// Pushes the wall along its normal by the distance, both its corners with it, so the walls meeting it stretch. With
    /// <paramref name="carry"/> the openings on those walls keep their distance from the pushed wall and move with it; without, they
    /// keep their place.
    /// </summary>
    public static void PushWall(BuildingStorey storey, int wallId, float distance, bool carry = true)
    {
        if (PlanGeometry.FindWall(storey, wallId) is not { } wall || MathF.Abs(distance) < Tiny)
        {
            return;
        }

        List<(PlanOpening Opening, PlanWall Wall, bool FromEnd, float Before)> held = [];
        foreach (PlanOpening opening in storey.Openings)
        {
            if (opening.Kind == OpeningKind.Floor || opening.Wall == wall.Id || PlanGeometry.FindWall(storey, opening.Wall) is not { } other)
            {
                continue;
            }

            bool atFrom = other.From == wall.From || other.From == wall.To;
            bool atTo = other.To == wall.From || other.To == wall.To;
            if (atFrom == atTo)
            {
                continue;
            }

            // The end whose distance stays: the pushed wall's end when carried, else the far end.
            bool fromEnd = atFrom == carry;
            float length = PlanGeometry.GetLength(PlanGeometry.GetPath(storey, other));
            float before = opening.Along;
            if (!fromEnd)
            {
                before = length - opening.Along;
            }

            held.Add((opening, other, fromEnd, before));
        }

        Vector2 normal = GetNormal(storey, wall);
        Vector2 offset = normal * distance;
        foreach (int cornerId in new[] { wall.From, wall.To })
        {
            if (PlanGeometry.FindCorner(storey, cornerId) is not { } corner)
            {
                continue;
            }

            corner.X += offset.X;
            corner.Z += offset.Y;
        }

        foreach ((PlanOpening opening, PlanWall other, bool fromEnd, float before) in held)
        {
            opening.Along = before;
            if (!fromEnd)
            {
                float length = PlanGeometry.GetLength(PlanGeometry.GetPath(storey, other));
                opening.Along = length - before;
            }
        }

        KeepOpenings(storey);
    }

    /// <summary>Moves the corner to the point; the walls meeting it follow.</summary>
    public static void MoveCorner(BuildingStorey storey, int cornerId, Vector2 point)
    {
        if (PlanGeometry.FindCorner(storey, cornerId) is not { } corner)
        {
            return;
        }

        corner.X = point.X;
        corner.Z = point.Y;
        KeepOpenings(storey);
    }

    /// <summary>Bends the wall so its middle stands <paramref name="bulge"/> to its left; 0 straightens it.</summary>
    public static void SetBulge(BuildingStorey storey, int wallId, float bulge)
    {
        if (PlanGeometry.FindWall(storey, wallId) is not { } wall)
        {
            return;
        }

        wall.Bulge = bulge;
        KeepOpenings(storey);
    }

    /// <summary>
    /// Splits the wall at the distance along it with a new corner: two walls of its kind, an arc's halves bent on the same circle, and
    /// each opening on the half it stands on. The new corner's id; null when the point is at an end.
    /// </summary>
    public static int? SplitWall(BuildingStorey storey, int wallId, float along)
    {
        if (PlanGeometry.FindWall(storey, wallId) is not { } wall)
        {
            return null;
        }

        List<Vector2> path = PlanGeometry.GetPath(storey, wall);
        float length = PlanGeometry.GetLength(path);
        if (along <= Join || along >= length - Join)
        {
            return null;
        }

        (Vector2 point, _) = PlanGeometry.GetAlong(path, along);
        PlanCorner corner = new() { Id = storey.NextId(), X = point.X, Z = point.Y };
        storey.Corners.Add(corner);
        PlanWall second = new()
        {
            Id = storey.NextId(),
            From = corner.Id,
            To = wall.To,
            Outside = wall.Outside,
            Thickness = wall.Thickness,
            Height = wall.Height,
            CustomHeight = wall.CustomHeight,
            Segments = wall.Segments,
        };
        float bulge = wall.Bulge;
        Vector2 start = path[0];
        Vector2 end = path[^1];
        wall.To = corner.Id;
        if (MathF.Abs(bulge) > Tiny)
        {
            float chord = Vector2.Distance(start, end) / 2;
            float radius = ((chord * chord) + (bulge * bulge)) / (2 * MathF.Abs(bulge));
            wall.Bulge = GetSagitta(radius, Vector2.Distance(start, point)) * MathF.Sign(bulge);
            second.Bulge = GetSagitta(radius, Vector2.Distance(point, end)) * MathF.Sign(bulge);
        }

        storey.Walls.Add(second);
        foreach (PlanOpening opening in storey.Openings)
        {
            if (opening.Kind != OpeningKind.Floor && opening.Wall == wall.Id && opening.Along > along)
            {
                opening.Wall = second.Id;
                opening.Along -= along;
            }
        }

        KeepOpenings(storey);
        return corner.Id;
    }

    private static float GetSagitta(float radius, float chord)
    {
        float half = chord / 2;
        float inside = Math.Max((radius * radius) - (half * half), 0);
        return radius - MathF.Sqrt(inside);
    }

    /// <summary>
    /// Draws a wall from one point to another: each end joins a corner or a wall near it (a wall is split there), and the new wall is
    /// split where it crosses other straight walls, so rooms close. The ids of the walls drawn, start first.
    /// </summary>
    public static List<int> AddWall(BuildingStorey storey, Vector2 from, Vector2 to, bool outside)
    {
        if (Vector2.Distance(from, to) < Join)
        {
            return [];
        }

        int start = JoinPoint(storey, from);
        int end = JoinPoint(storey, to);
        if (start == end)
        {
            return [];
        }

        Vector2 a = PlanGeometry.GetPoint(PlanGeometry.FindCorner(storey, start)!);
        Vector2 b = PlanGeometry.GetPoint(PlanGeometry.FindCorner(storey, end)!);

        // Where the new wall crosses straight walls, both are split, the new one at each crossing in order from its start.
        List<(float T, int Corner)> crossings = [];
        foreach (PlanWall other in storey.Walls.ToList())
        {
            if (MathF.Abs(other.Bulge) > Tiny || other.From == start || other.To == start || other.From == end || other.To == end)
            {
                continue;
            }

            Vector2 c = PlanGeometry.GetPoint(PlanGeometry.FindCorner(storey, other.From)!);
            Vector2 d = PlanGeometry.GetPoint(PlanGeometry.FindCorner(storey, other.To)!);
            if (Cross(a, b, c, d) is not { } hit)
            {
                continue;
            }

            float along = Vector2.Distance(c, hit.Point);
            if (SplitWall(storey, other.Id, along) is { } corner)
            {
                crossings.Add((hit.T, corner));
            }
        }

        crossings.Sort(static (first, second) => first.T.CompareTo(second.T));
        List<int> chain = [start, .. crossings.Select(static crossing => crossing.Corner), end];
        List<int> walls = [];
        for (int index = 1; index < chain.Count; index++)
        {
            if (chain[index - 1] == chain[index] || HasWall(storey, chain[index - 1], chain[index]))
            {
                continue;
            }

            PlanWall wall = new() { Id = storey.NextId(), From = chain[index - 1], To = chain[index], Outside = outside };
            storey.Walls.Add(wall);
            walls.Add(wall.Id);
        }

        SyncRooms(storey);
        return walls;
    }

    /// <summary>Draws a rectangular room between two opposite corners: four inside walls that join the walls they meet.</summary>
    public static void AddRoom(BuildingStorey storey, Vector2 first, Vector2 second, string name)
    {
        Vector2 low = Vector2.Min(first, second);
        Vector2 high = Vector2.Max(first, second);
        if (high.X - low.X < 2 * Join || high.Y - low.Y < 2 * Join)
        {
            return;
        }

        Vector2[] corners = [low, new(high.X, low.Y), high, new(low.X, high.Y)];
        for (int index = 0; index < corners.Length; index++)
        {
            AddWall(storey, corners[index], corners[(index + 1) % corners.Length], outside: false);
        }

        Vector2 middle = (low + high) / 2;
        storey.Rooms.RemoveAll(room => Polygons.Contains(corners, new Vector2(room.X, room.Z)));
        storey.Rooms.Add(new PlanRoom { Name = name, X = middle.X, Z = middle.Y });
        SyncRooms(storey);
    }

    /// <summary>Takes the wall out with the openings cut through it; corners left with no wall go too.</summary>
    public static void RemoveWall(BuildingStorey storey, int wallId)
    {
        if (PlanGeometry.FindWall(storey, wallId) is not { } wall)
        {
            return;
        }

        storey.Walls.Remove(wall);
        storey.Openings.RemoveAll(opening => opening.Kind != OpeningKind.Floor && opening.Wall == wallId);
        storey.Corners.RemoveAll(corner => PlanGeometry.GetWallsAt(storey, corner.Id).Count == 0);
        SyncRooms(storey);
    }

    /// <summary>
    /// Takes the corner out: with two walls meeting there they become one wall (straight) from end to end, keeping the first's openings;
    /// otherwise every wall meeting it goes.
    /// </summary>
    public static void RemoveCorner(BuildingStorey storey, int cornerId)
    {
        List<PlanWall> walls = PlanGeometry.GetWallsAt(storey, cornerId);
        if (walls.Count == 2)
        {
            PlanWall first = walls[0];
            PlanWall second = walls[1];
            int far = second.From == cornerId ? second.To : second.From;
            if (first.From == cornerId)
            {
                first.From = far;
            }
            else
            {
                first.To = far;
            }

            first.Bulge = 0;
            storey.Walls.Remove(second);
            storey.Openings.RemoveAll(opening => opening.Kind != OpeningKind.Floor && opening.Wall == second.Id);
        }
        else
        {
            foreach (PlanWall wall in walls)
            {
                storey.Walls.Remove(wall);
                storey.Openings.RemoveAll(opening => opening.Kind != OpeningKind.Floor && opening.Wall == wall.Id);
            }
        }

        storey.Corners.RemoveAll(corner => corner.Id == cornerId || PlanGeometry.GetWallsAt(storey, corner.Id).Count == 0);
        KeepOpenings(storey);
        SyncRooms(storey);
    }

    /// <summary>
    /// Keeps a room record for every space the walls close: a space without one gets a new room named in order, a record whose space is
    /// gone is dropped, and each record's point moves well inside its space.
    /// </summary>
    public static void SyncRooms(BuildingStorey storey)
    {
        List<RoomShape> shapes = PlanGeometry.FindRooms(storey);
        HashSet<int> kept = [];
        List<PlanRoom> rooms = [];
        foreach (RoomShape shape in shapes)
        {
            Vector2 inside = Polygons.InteriorPoint(shape.Polygon);
            if (shape.Record >= 0)
            {
                PlanRoom room = storey.Rooms[shape.Record];
                kept.Add(shape.Record);
                if (!Polygons.Contains(shape.Polygon, new Vector2(room.X, room.Z)))
                {
                    room.X = inside.X;
                    room.Z = inside.Y;
                }

                rooms.Add(room);
                continue;
            }

            rooms.Add(new PlanRoom { Name = NextRoomName(storey, rooms), X = inside.X, Z = inside.Y });
        }

        storey.Rooms = rooms;
    }

    private static string NextRoomName(BuildingStorey storey, List<PlanRoom> rooms)
    {
        for (int number = 1; ; number++)
        {
            string name = $"Room {number}";
            if (!rooms.Exists(room => room.Name == name) && !storey.Rooms.Exists(room => room.Name == name))
            {
                return name;
            }
        }
    }

    /// <summary>Adds a pillar standing at the point; its id.</summary>
    public static int AddPillar(BuildingStorey storey, Vector2 point, float size, bool round)
    {
        PlanPillar pillar = new() { Id = storey.NextId(), X = point.X, Z = point.Y, Size = size, Round = round };
        storey.Pillars.Add(pillar);
        return pillar.Id;
    }

    /// <summary>
    /// Adds an opening of the kind through the wall, centred at the distance along it, sized for its kind and held clear of the wall's
    /// ends; a window stands at sill height. Its id.
    /// </summary>
    public static int AddOpening(BuildingStorey storey, int wallId, float along, OpeningKind kind)
    {
        PlanOpening opening = new() { Id = storey.NextId(), Kind = kind, Wall = wallId, Along = along };
        switch (kind)
        {
            case OpeningKind.Window:
                opening.Width = 1.2f;
                opening.Height = 1.2f;
                opening.Lift = 0.9f;
                opening.Name = NextName(storey, "Window");
                break;
            case OpeningKind.Doorway:
                opening.Width = 1.2f;
                opening.Height = 2.2f;
                opening.Name = NextName(storey, "Doorway");
                break;
            default:
                opening.Width = 1;
                opening.Height = 2.1f;
                opening.Name = NextName(storey, "Door");
                break;
        }

        storey.Openings.Add(opening);
        KeepOpenings(storey);
        return opening.Id;
    }

    /// <summary>Adds a hole in the floor centred at the point, of the width and depth; its id.</summary>
    public static int AddHole(BuildingStorey storey, Vector2 point, float width, float depth)
    {
        PlanOpening hole = new() { Id = storey.NextId(), Kind = OpeningKind.Floor, X = point.X, Z = point.Y, Width = width, Height = depth, Name = NextName(storey, "Hole") };
        storey.Openings.Add(hole);
        return hole.Id;
    }

    /// <summary>The name of the next opening of the kind: Door 2, Window 3.</summary>
    public static string NextName(BuildingStorey storey, string kind)
    {
        for (int number = 1; ; number++)
        {
            string name = $"{kind} {number}";
            if (!storey.Openings.Exists(opening => opening.Name == name))
            {
                return name;
            }
        }
    }

    /// <summary>Holds every opening on its wall: clear of its ends, and inside the wall's length when the wall got shorter.</summary>
    public static void KeepOpenings(BuildingStorey storey)
    {
        foreach (PlanOpening opening in storey.Openings)
        {
            if (opening.Kind == OpeningKind.Floor || PlanGeometry.FindWall(storey, opening.Wall) is not { } wall)
            {
                continue;
            }

            List<Vector2> path = PlanGeometry.GetPath(storey, wall);
            float length = PlanGeometry.GetLength(path);
            float half = (opening.Width / 2) + opening.Margin + OpeningShape.EdgeClearance;
            if (length <= 2 * half)
            {
                opening.Along = length / 2;
                continue;
            }

            opening.Along = Math.Clamp(opening.Along, half, length - half);
        }
    }

    /// <summary>A corner at the point: an existing one near it, a split of a wall near it, or a new one.</summary>
    private static int JoinPoint(BuildingStorey storey, Vector2 point)
    {
        if (FindCorner(storey, point, Join) is { } corner)
        {
            return corner.Id;
        }

        if (FindWall(storey, point, Join) is { } hit && SplitWall(storey, hit.Wall.Id, hit.Along) is { } split)
        {
            return split;
        }

        PlanCorner added = new() { Id = storey.NextId(), X = point.X, Z = point.Y };
        storey.Corners.Add(added);
        return added.Id;
    }

    private static bool HasWall(BuildingStorey storey, int first, int second)
    {
        return storey.Walls.Exists(wall => (wall.From == first && wall.To == second) || (wall.From == second && wall.To == first));
    }

    /// <summary>Where segment a-b crosses segment c-d inside both (not at their ends), with how far along a-b; null when they don't.</summary>
    private static (Vector2 Point, float T)? Cross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        Vector2 r = b - a;
        Vector2 s = d - c;
        float denominator = Polygons.Cross(r, s);
        if (MathF.Abs(denominator) < Tiny)
        {
            return null;
        }

        float t = Polygons.Cross(c - a, s) / denominator;
        float u = Polygons.Cross(c - a, r) / denominator;
        float margin = 0.01f;
        if (t <= margin || t >= 1 - margin || u <= margin || u >= 1 - margin)
        {
            return null;
        }

        return (a + (r * t), t);
    }
}
