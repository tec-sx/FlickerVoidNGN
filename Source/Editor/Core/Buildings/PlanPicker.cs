using System.Numerics;

using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>
/// Finds what a ray meets first of a storey as it stands, in the building's space: an opening in its wall, a wall by any of its
/// faces, a pillar, stairs by their steps, an elevator by its square on the floor, a hole in the floor, or the room whose floor the
/// ray lands on. So a click anywhere on a thing picks it, whatever the view's angle.
/// </summary>
public static class PlanPicker
{
    /// <summary>How far an opening stands out of its wall's faces for the ray, so it is met before the wall.</summary>
    private const float OpeningProud = 0.03f;

    /// <summary>How thick the slab is that stands for a thing lying on the floor.</summary>
    private const float OnFloor = 0.06f;

    /// <summary>The nearest thing the ray meets of the storey at the level, of the kinds taken (all when null).</summary>
    public static PlanHit? Pick(BuildingFile file, int level, Ray ray, Func<PlanPickKind, bool>? takes = null)
    {
        if (file.FindStorey(level) is not { } storey)
        {
            return null;
        }

        PlanHit? best = null;
        float floor = file.GetFloorHeight(level);
        if (Takes(takes, PlanPickKind.Opening))
        {
            foreach (PlanOpening opening in storey.Openings)
            {
                if (HitOpening(file, storey, opening, ray, floor, out float distance))
                {
                    Keep(ref best, new PlanPick(PlanPickKind.Opening, level, opening.Id), ray, distance);
                }
            }
        }

        if (Takes(takes, PlanPickKind.Wall))
        {
            foreach (PlanWall wall in storey.Walls)
            {
                if (HitWall(storey, wall, ray, floor, out float distance))
                {
                    Keep(ref best, new PlanPick(PlanPickKind.Wall, level, wall.Id), ray, distance);
                }
            }
        }

        if (Takes(takes, PlanPickKind.Pillar))
        {
            float height = storey.Height - storey.Floor;
            foreach (PlanPillar pillar in storey.Pillars)
            {
                Vector3 half = new(pillar.Size / 2, height / 2, pillar.Size / 2);
                Vector3 middle = new(pillar.X, floor + (height / 2), pillar.Z);
                if (ray.IntersectBox(middle - half, middle + half, out float distance))
                {
                    Keep(ref best, new PlanPick(PlanPickKind.Pillar, level, pillar.Id), ray, distance);
                }
            }
        }

        if (Takes(takes, PlanPickKind.Stairs))
        {
            foreach (BuildingStairs stairs in file.Stairs)
            {
                if (stairs.Level == level && HitStairs(file, stairs, ray, out float distance))
                {
                    Keep(ref best, new PlanPick(PlanPickKind.Stairs, level, stairs.Id), ray, distance);
                }
            }
        }

        if (Takes(takes, PlanPickKind.Elevator))
        {
            foreach (BuildingElevator elevator in file.Elevators)
            {
                if (HitElevator(file, elevator, level, ray, out float distance))
                {
                    Keep(ref best, new PlanPick(PlanPickKind.Elevator, level, elevator.Id), ray, distance);
                }
            }
        }

        if (Takes(takes, PlanPickKind.Room) && TryGetFloorPoint(ray, floor, out Vector3 landed, out float along))
        {
            Vector2 point = new(landed.X, landed.Z);
            foreach (RoomShape room in PlanGeometry.FindRooms(storey))
            {
                if (room.Record >= 0 && Polygons.Contains(room.Polygon, point))
                {
                    Keep(ref best, new PlanPick(PlanPickKind.Room, level, 0, storey.Rooms[room.Record].Name), ray, along);
                }
            }
        }

        return best;
    }

    /// <summary>Where the ray meets the level plane at a height, in front of its origin.</summary>
    public static bool TryGetFloorPoint(Ray ray, float height, out Vector3 point, out float distance)
    {
        point = default;
        distance = 0;
        if (MathF.Abs(ray.Direction.Y) < 1e-5f)
        {
            return false;
        }

        distance = (height - ray.Origin.Y) / ray.Direction.Y;
        if (distance < 0)
        {
            return false;
        }

        point = ray.GetPoint(distance);
        return true;
    }

    private static bool Takes(Func<PlanPickKind, bool>? takes, PlanPickKind kind) => takes is null || takes(kind);

    private static void Keep(ref PlanHit? best, PlanPick pick, Ray ray, float distance)
    {
        if (best is { } held && held.Distance <= distance)
        {
            return;
        }

        best = new PlanHit(pick, distance, ray.GetPoint(distance));
    }

    private static bool HitOpening(BuildingFile file, BuildingStorey storey, PlanOpening opening, Ray ray, float floor, out float distance)
    {
        if (opening.Kind == OpeningKind.Floor)
        {
            Vector3 half = new(opening.Width / 2, OnFloor, opening.Height / 2);
            Vector3 middle = new(opening.X, floor, opening.Z);
            return ray.IntersectBox(middle - half, middle + half, out distance);
        }

        distance = 0;
        if (!OpeningShape.TryGetFrame(file, storey, opening, out OpeningFrame frame))
        {
            return false;
        }

        float reach = (frame.WallThickness / 2) + OpeningProud;
        Vector3 min = new(-opening.Width / 2, 0, -reach);
        Vector3 max = new(opening.Width / 2, opening.Height, reach);
        return HitBox(ray, frame.Origin, frame.Along, frame.Out, min, max, out distance);
    }

    /// <summary>Each stretch of the wall's line is a box as thick and as high as the wall.</summary>
    private static bool HitWall(BuildingStorey storey, PlanWall wall, Ray ray, float floor, out float distance)
    {
        distance = float.MaxValue;
        List<Vector2> path = PlanGeometry.GetPath(storey, wall);
        float half = PlanGeometry.GetThickness(storey, wall) / 2;
        float height = PlanGeometry.GetHeight(storey, wall);
        bool hit = false;
        for (int index = 1; index < path.Count; index++)
        {
            Vector2 from = path[index - 1];
            Vector2 to = path[index];
            float length = Vector2.Distance(from, to);
            if (length < 1e-5f)
            {
                continue;
            }

            Vector2 direction = (to - from) / length;
            Vector3 along = new(direction.X, 0, direction.Y);
            Vector3 across = new(-direction.Y, 0, direction.X);
            Vector3 origin = new(from.X, floor, from.Y);
            Vector3 min = new(-half, 0, -half);
            Vector3 max = new(length + half, height, half);
            if (HitBox(ray, origin, along, across, min, max, out float found) && found < distance)
            {
                distance = found;
                hit = true;
            }
        }

        return hit;
    }

    private static bool HitStairs(BuildingFile file, BuildingStairs stairs, Ray ray, out float distance)
    {
        distance = float.MaxValue;
        StairsLayout layout = BuildingMaker.GetStairsLayout(file, stairs);
        Transform place = BuildingMaker.GetStairsPlace(file, stairs);
        if (!Matrix4x4.Invert(place.ToMatrix(), out Matrix4x4 toStairs))
        {
            return false;
        }

        Ray local = ray.Transform(toStairs);
        bool hit = false;
        foreach ((Vector2 low, Vector2 high, float top) in layout.Blocks)
        {
            Vector3 min = new(low.X, 0, low.Y);
            Vector3 max = new(high.X, top, high.Y);
            if (local.IntersectBox(min, max, out float found) && found < distance)
            {
                distance = found;
                hit = true;
            }
        }

        return hit;
    }

    /// <summary>The elevator's square on the storey's floor, turned as the elevator is, and its platform where it rests.</summary>
    private static bool HitElevator(BuildingFile file, BuildingElevator elevator, int level, Ray ray, out float distance)
    {
        distance = float.MaxValue;
        (int from, int to) = BuildingMaker.GetElevatorSpan(file, elevator);
        if (level < from || level > to)
        {
            return false;
        }

        Vector3 half = new(elevator.Size / 2, OnFloor, elevator.Size / 2);
        bool hit = false;
        int[] levels = [level, from];
        foreach (int stop in levels)
        {
            Transform place = BuildingMaker.GetElevatorPlace(file, elevator, stop);
            if (!Matrix4x4.Invert(place.ToMatrix(), out Matrix4x4 toShaft))
            {
                continue;
            }

            Ray local = ray.Transform(toShaft);
            if (local.IntersectBox(-half, half, out float found) && found < distance)
            {
                distance = found;
                hit = true;
            }
        }

        return hit;
    }

    /// <summary>Meets a box given in a frame standing upright: an origin, the way along and the way across, both level.</summary>
    private static bool HitBox(Ray ray, Vector3 origin, Vector3 along, Vector3 across, Vector3 min, Vector3 max, out float distance)
    {
        Vector3 from = ray.Origin - origin;
        Vector3 localOrigin = new(Vector3.Dot(from, along), from.Y, Vector3.Dot(from, across));
        Vector3 localDirection = new(Vector3.Dot(ray.Direction, along), ray.Direction.Y, Vector3.Dot(ray.Direction, across));
        Ray local = new(localOrigin, localDirection);
        return local.IntersectBox(min, max, out distance);
    }
}
