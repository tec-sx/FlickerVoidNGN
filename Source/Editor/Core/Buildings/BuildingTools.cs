using System.Numerics;

using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>
/// The building creator's edits of a whole building: making its parts from its plans again (openings cut with Booleans when the native
/// library is here), its storeys (added above, a cellar below, duplicated, taken away), stairs, elevators and camera shots, and the
/// places new ones stand.
/// </summary>
public static class BuildingTools
{
    /// <summary>Cuts one solid out of another with the Booleans' native library.</summary>
    private static readonly Func<SolidMesh, SolidMesh, SolidMesh?> Subtract = static (solid, cutter) => SolidBoolean.Combine(solid, cutter, BooleanOperation.Subtract);

    /// <summary>Makes every part of the building from its plans again.</summary>
    public static void Rebuild(BuildingFile building)
    {
        Func<SolidMesh, SolidMesh, SolidMesh?>? subtract = null;
        if (SolidBoolean.IsAvailable)
        {
            subtract = Subtract;
        }

        BuildingMaker.Make(building, subtract);
    }

    /// <summary>Adds a storey on top of the building, its plan a copy of the top storey's walls and rooms without their openings; its level.</summary>
    public static int AddStoreyAbove(BuildingFile building)
    {
        if (building.StoreysAbove >= BuildingFile.MaxStoreys)
        {
            return building.Storeys[^1].Level;
        }

        BuildingStorey top = building.Storeys[^1];
        BuildingStorey storey = CopyPlan(top, top.Level + 1, openings: false);
        storey.Rooms = [new PlanRoom { Name = "Landing", X = 0, Z = 0 }];
        building.Storeys.Add(storey);
        PlanEdits.SyncRooms(storey);
        return storey.Level;
    }

    /// <summary>Gives the building a cellar under its ground floor, the ground floor's outside walls round one room; its level.</summary>
    public static int AddCellar(BuildingFile building)
    {
        if (building.HasCellar)
        {
            return -1;
        }

        BuildingStorey ground = building.Storeys[0];
        BuildingStorey cellar = CopyPlan(ground, -1, openings: false);
        cellar.Walls.RemoveAll(static wall => !wall.Outside);
        cellar.Corners.RemoveAll(corner => PlanGeometry.GetWallsAt(cellar, corner.Id).Count == 0);
        cellar.Rooms = [new PlanRoom { Name = "Cellar", X = 0, Z = 0 }];
        cellar.Pillars = [];
        building.Storeys.Insert(0, cellar);
        PlanEdits.SyncRooms(cellar);
        return -1;
    }

    /// <summary>Adds a copy of the storey on top of the building, with its walls, rooms, pillars and openings; its level.</summary>
    public static int DuplicateStorey(BuildingFile building, int level)
    {
        if (building.FindStorey(level) is not { } source || building.StoreysAbove >= BuildingFile.MaxStoreys)
        {
            return level;
        }

        int top = building.Storeys[^1].Level + 1;
        BuildingStorey copy = CopyPlan(source, top, openings: true);
        copy.Openings.RemoveAll(static opening => opening.Kind == OpeningKind.Door && opening.Locked);
        building.Storeys.Add(copy);
        return top;
    }

    /// <summary>
    /// Takes the storey away: the ground floor stays; storeys above move down a level, and stairs, elevator stops and shots on it go
    /// with it.
    /// </summary>
    public static void RemoveStorey(BuildingFile building, int level)
    {
        if (level == 0 || building.FindStorey(level) is not { } storey)
        {
            return;
        }

        building.Storeys.Remove(storey);
        building.Stairs.RemoveAll(stairs => stairs.Level == level || stairs.Level == level - 1);
        building.Shots.RemoveAll(shot => shot.Level == level);
        foreach (BuildingShot shot in building.Shots)
        {
            shot.Triggers.RemoveAll(trigger => trigger.Target is TriggerTarget.Room or TriggerTarget.Opening && trigger.Level == level);
        }

        if (level > 0)
        {
            foreach (BuildingStorey above in building.Storeys.Where(above => above.Level > level))
            {
                above.Level--;
            }

            foreach (BuildingStairs stairs in building.Stairs.Where(stairs => stairs.Level > level))
            {
                stairs.Level--;
            }
        }

        int lowest = building.Storeys[0].Level;
        int highest = building.Storeys[^1].Level;
        foreach (BuildingElevator elevator in building.Elevators)
        {
            elevator.From = Math.Clamp(elevator.From, lowest, highest);
            elevator.To = Math.Clamp(elevator.To, lowest, highest);
        }

        building.Elevators.RemoveAll(static elevator => elevator.From == elevator.To);
    }

    /// <summary>Gives the building that many storeys on the ground and a cellar or none, adding at the top and taking off the top.</summary>
    public static void SetStoreys(BuildingFile building, int above, bool cellar)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(above, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(above, BuildingFile.MaxStoreys);
        while (building.StoreysAbove < above)
        {
            AddStoreyAbove(building);
        }

        while (building.StoreysAbove > above)
        {
            RemoveStorey(building, building.Storeys[^1].Level);
        }

        if (cellar && !building.HasCellar)
        {
            AddCellar(building);
        }
        else if (!cellar && building.HasCellar)
        {
            RemoveStorey(building, -1);
        }
    }

    private static BuildingStorey CopyPlan(BuildingStorey source, int level, bool openings)
    {
        BuildingStorey copy = new()
        {
            Level = level,
            Height = source.Height,
            Wall = source.Wall,
            Floor = source.Floor,
            Ambient = source.Ambient,
            CornerPillars = source.CornerPillars,
            CornerPillarSize = source.CornerPillarSize,
            Corners = [.. source.Corners.Select(static corner => new PlanCorner { Id = corner.Id, X = corner.X, Z = corner.Z })],
            Walls = [.. source.Walls.Select(static wall => new PlanWall { Id = wall.Id, From = wall.From, To = wall.To, Outside = wall.Outside, Thickness = wall.Thickness, Height = wall.Height, CustomHeight = wall.CustomHeight, Bulge = wall.Bulge, Segments = wall.Segments })],
            Rooms = [.. source.Rooms.Select(static room => new PlanRoom { Name = room.Name, X = room.X, Z = room.Z, Ambient = room.Ambient })],
            Pillars = [.. source.Pillars.Select(static pillar => new PlanPillar { Id = pillar.Id, X = pillar.X, Z = pillar.Z, Size = pillar.Size, Round = pillar.Round })],
        };
        if (openings)
        {
            copy.Openings = [.. source.Openings.Select(static opening => opening.Copy())];
        }

        return copy;
    }

    /// <summary>
    /// Adds a flight of stairs from the level to the one above, its foot's middle at the point, climbing the heading (degrees from +Z
    /// toward +X); its id, or null when no storey is above.
    /// </summary>
    public static int? AddStairs(BuildingFile building, int level, Vector2 foot, float heading, StairsKind kind)
    {
        if (building.FindStorey(level) is null || building.FindStorey(level + 1) is null)
        {
            return null;
        }

        int id = 1 + building.Stairs.Select(static stairs => stairs.Id).DefaultIfEmpty(0).Max();
        BuildingStairs added = new() { Id = id, Level = level, X = foot.X, Z = foot.Y, Heading = heading, Kind = kind };
        building.Stairs.Add(added);
        return id;
    }

    /// <summary>Adds an elevator with its shaft's middle at the point, stopping at every storey; its id.</summary>
    public static int AddElevator(BuildingFile building, Vector2 place, float heading)
    {
        int id = 1 + building.Elevators.Select(static elevator => elevator.Id).DefaultIfEmpty(0).Max();
        BuildingElevator added = new() { Id = id, X = place.X, Z = place.Y, Heading = heading, From = building.Storeys[0].Level, To = building.Storeys[^1].Level };
        building.Elevators.Add(added);
        return id;
    }

    /// <summary>Adds a shot on the level standing at the place and turned as the rotation says, named in order; its id.</summary>
    public static int AddShot(BuildingFile building, int level, Vector3 position, Quaternion rotation)
    {
        int id = 1 + building.Shots.Select(static shot => shot.Id).DefaultIfEmpty(0).Max();
        string name = $"Shot {id}";
        BuildingShot added = new() { Id = id, Name = name, Level = level, Position = position, Rotation = rotation };
        building.Shots.Add(added);
        return id;
    }

    /// <summary>
    /// The way a shot looks down at a point from a direction: turned about the vertical by <paramref name="yaw"/> and down by
    /// <paramref name="pitch"/>, standing <paramref name="distance"/> back from the point.
    /// </summary>
    public static (Vector3 Position, Quaternion Rotation) LookAt(Vector3 point, float yaw, float pitch, float distance)
    {
        Quaternion rotation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0);
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        return (point - (forward * distance), rotation);
    }

    /// <summary>Where a new building's flight of stairs from the level starts: against its left wall, near the back.</summary>
    public static Vector2 GetStairsFoot(BuildingFile building, int level)
    {
        int lane = ((level % 2) + 2) % 2;
        float x = (-building.Size.X / 2) + building.Wall + 0.8f + (lane * 1.6f);
        float z = (-building.Size.Z / 2) + building.Wall + 1;
        return new Vector2(x, z);
    }

    /// <summary>Where a new elevator stands: against the right wall, halfway along it.</summary>
    public static Vector2 GetElevatorPlace(BuildingFile building)
    {
        float x = (building.Size.X / 2) - building.Wall - 1.2f;
        return new Vector2(x, 0);
    }
}
