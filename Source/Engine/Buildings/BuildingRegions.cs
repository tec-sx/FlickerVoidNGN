using System.Numerics;

using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// The boxes of a building's spaces, in the building's space: each storey's inside and each room's, standing on their floor up to
/// the ceiling, and the boxes that set off its camera triggers: a room, the way through an opening, a flight of stairs and an
/// elevator's shaft.
/// </summary>
public static class BuildingRegions
{
    /// <summary>How far past a wall's faces the way through an opening reaches on each side.</summary>
    public const float OpeningReach = 0.8f;

    /// <summary>How high above the stairs' top step and an elevator's top stop their boxes reach.</summary>
    public const float Headroom = 2;

    /// <summary>The storey's inside, from the top of its floor to its ceiling, inside its outside walls.</summary>
    public static BuildingRegion? GetStorey(BuildingFile file, BuildingStorey storey)
    {
        List<Vector2> inside = PlanGeometry.GetInsideOutline(storey);
        return Stand(file, storey, inside, 0);
    }

    /// <summary>Each room the storey's walls close with its name (the matching room's, or empty), inside its walls.</summary>
    public static List<(string Name, float Ambient, BuildingRegion Region)> GetRooms(BuildingFile file, BuildingStorey storey)
    {
        List<(string, float, BuildingRegion)> rooms = [];
        foreach (RoomShape shape in PlanGeometry.FindRooms(storey))
        {
            string name = string.Empty;
            float ambient = storey.Ambient;
            if (shape.Record >= 0)
            {
                PlanRoom room = storey.Rooms[shape.Record];
                name = room.Name;
                ambient = room.Ambient;
            }

            if (Stand(file, storey, shape.Polygon, storey.Wall / 2) is { } region)
            {
                rooms.Add((name, ambient, region));
            }
        }

        return rooms;
    }

    /// <summary>The box that sets off the trigger, or null when what it names is gone.</summary>
    public static BuildingRegion? GetTrigger(BuildingFile file, ShotTrigger trigger)
    {
        switch (trigger.Target)
        {
            case TriggerTarget.Room:
                return GetRoom(file, trigger.Level, trigger.Room);
            case TriggerTarget.Opening:
                return GetOpening(file, trigger.Level, trigger.Id);
            case TriggerTarget.Stairs:
                return GetStairs(file, trigger.Id);
            default:
                return GetElevator(file, trigger.Id);
        }
    }

    public static BuildingRegion? GetRoom(BuildingFile file, int level, string name)
    {
        if (file.FindStorey(level) is not { } storey)
        {
            return null;
        }

        foreach ((string room, _, BuildingRegion region) in GetRooms(file, storey))
        {
            if (string.Equals(room, name, StringComparison.OrdinalIgnoreCase))
            {
                return region;
            }
        }

        return null;
    }

    /// <summary>The way through the opening: its width and height, reaching out of the wall on both sides.</summary>
    public static BuildingRegion? GetOpening(BuildingFile file, int level, int id)
    {
        if (file.FindStorey(level) is not { } storey || storey.Openings.Find(opening => opening.Id == id) is not { } opening)
        {
            return null;
        }

        if (opening.Kind == OpeningKind.Floor)
        {
            Transform hole = new() { Position = new Vector3(opening.X, file.GetBase(level), opening.Z) };
            Vector3 holeSize = new(opening.Width, Headroom * 2, opening.Height);
            return new BuildingRegion(hole, holeSize, Vector3.Zero);
        }

        if (!OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame))
        {
            return null;
        }

        float turn = MathF.Atan2(-frame.Along.Z, frame.Along.X);
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, turn);
        Transform place = new() { Position = frame.Origin, Rotation = rotation };
        Vector3 size = new(opening.Width, opening.Height, frame.WallThickness + (2 * OpeningReach));
        return new BuildingRegion(place, size, new Vector3(0, opening.Height / 2, 0));
    }

    /// <summary>The flight's steps and landing, up to a head's height over the top step.</summary>
    public static BuildingRegion? GetStairs(BuildingFile file, int id)
    {
        if (file.Stairs.Find(stairs => stairs.Id == id) is not { } stairs)
        {
            return null;
        }

        StairsLayout layout = BuildingMaker.GetStairsLayout(file, stairs);
        if (layout.Blocks.Count == 0)
        {
            return null;
        }

        Vector2 low = layout.Blocks[0].Low;
        Vector2 high = layout.Blocks[0].High;
        float top = 0;
        foreach ((Vector2 blockLow, Vector2 blockHigh, float blockTop) in layout.Blocks)
        {
            low = Vector2.Min(low, blockLow);
            high = Vector2.Max(high, blockHigh);
            top = MathF.Max(top, blockTop);
        }

        Transform place = BuildingMaker.GetStairsPlace(file, stairs);
        float height = top + Headroom;
        Vector2 middle = (low + high) / 2;
        Vector2 across = high - low;
        return new BuildingRegion(place, new Vector3(across.X, height, across.Y), new Vector3(middle.X, height / 2, middle.Y));
    }

    /// <summary>The shaft, from the lowest stop's floor to a head's height over the highest.</summary>
    public static BuildingRegion? GetElevator(BuildingFile file, int id)
    {
        if (file.Elevators.Find(elevator => elevator.Id == id) is not { } elevator || file.Storeys.Count == 0)
        {
            return null;
        }

        (int from, int to) = BuildingMaker.GetElevatorSpan(file, elevator);
        float bottom = file.GetFloorHeight(from);
        float height = file.GetFloorHeight(to) - bottom + Headroom;
        Transform place = BuildingMaker.GetElevatorPlace(file, elevator, from);
        Vector3 size = new(elevator.Size, height, elevator.Size);
        return new BuildingRegion(place, size, new Vector3(0, height / 2, 0));
    }

    /// <summary>The box round the polygon narrowed by <paramref name="inset"/>, from the top of the storey's floor to its ceiling, standing on its middle.</summary>
    private static BuildingRegion? Stand(BuildingFile file, BuildingStorey storey, IReadOnlyList<Vector2> polygon, float inset)
    {
        if (polygon.Count < 3)
        {
            return null;
        }

        Vector2 low = polygon[0];
        Vector2 high = polygon[0];
        foreach (Vector2 point in polygon)
        {
            low = Vector2.Min(low, point);
            high = Vector2.Max(high, point);
        }

        low += new Vector2(inset);
        high -= new Vector2(inset);
        Vector2 across = Vector2.Max(high - low, new Vector2(0.1f));
        Vector2 middle = (low + high) / 2;
        float height = storey.Height - storey.Floor;
        Transform place = new() { Position = new Vector3(middle.X, file.GetFloorHeight(storey.Level), middle.Y) };
        return new BuildingRegion(place, new Vector3(across.X, height, across.Y), new Vector3(0, height / 2, 0));
    }
}
