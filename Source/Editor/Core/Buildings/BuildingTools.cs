using System.Numerics;

using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>
/// The building creator's tools that change more than one part: stairs with the opening they come up through, and an elevator with
/// the shaft its platform climbs. The openings are cut from the storeys' shells with Booleans, and left uncut where those are missing.
/// </summary>
public static class BuildingTools
{
    /// <summary>How far an opening reaches past what comes through it, on every side, in metres.</summary>
    private const float Clearance = 0.1f;

    private const float StairsWidth = 1.2f;
    private const float PlatformSize = 1.6f;
    private const float EntranceWidth = 1.2f;
    private const float EntranceHeight = 2.1f;

    /// <summary>How far a new flight of stairs runs along the floor, in metres.</summary>
    public const float StairsRun = 3.6f;

    /// <summary>How much floor is left between the back wall and a new flight's foot, to step onto it from, in metres.</summary>
    private const float Landing = 1;

    /// <summary>
    /// Where a new flight from the level starts: against the left wall, in one of two lanes by the level, so each flight's foot
    /// stands clear of the opening the flight below comes up through.
    /// </summary>
    public static Vector2 GetStairsFoot(BuildingFile building, int level)
    {
        int lane = ((level % 2) + 2) % 2;
        float x = (-building.Size.X / 2) + building.Wall + Clearance + (StairsWidth / 2) + (lane * (StairsWidth + (3 * Clearance)));
        float z = (-building.Size.Z / 2) + building.Wall + Landing;
        return new Vector2(x, z);
    }

    /// <summary>Where a new elevator stands: against the right wall, halfway along it.</summary>
    public static Vector2 GetElevatorPlace(BuildingFile building)
    {
        float x = (building.Size.X / 2) - building.Wall - (2 * Clearance) - (PlatformSize / 2);
        return new Vector2(x, 0);
    }

    /// <summary>
    /// Opens a doorway in the middle of the ground floor's front wall (the one toward -Z), from the floor up, so a new building can
    /// be walked into; a door can be hung in it later.
    /// </summary>
    public static void AddEntrance(BuildingFile building)
    {
        BuildingStorey? ground = building.Storeys.Find(static storey => storey.Level == 0);
        if (ground is null)
        {
            return;
        }

        Vector3 size = new(EntranceWidth, EntranceHeight, building.Wall + (2 * Clearance));
        Vector3 foot = new(0, building.GetFloorHeight(0), (-building.Size.Z / 2) + (building.Wall / 2));
        Cut(ground.Shell, size, foot);
    }

    /// <summary>
    /// Gives the building that many storeys on the ground and a cellar or none, then keeps what crosses them in step: flights of
    /// stairs that lost a floor go, and an elevator rests on the new lowest floor with its shaft cut through the new ones.
    /// </summary>
    public static void SetStoreys(BuildingFile building, int above, bool cellar)
    {
        List<BuildingStorey> before = [.. building.Storeys];
        building.SetStoreys(above, cellar);
        building.Stairs.RemoveAll(stairs => !Climbs(building, stairs));
        if (building.Elevator is not { } elevator)
        {
            return;
        }

        Vector3 position = elevator.Platform.Place.Position;
        Vector2 place = new(position.X, position.Z);
        float speed = elevator.Speed;
        building.SetElevator(place);
        building.Elevator.Speed = speed;
        for (int index = 1; index < building.Storeys.Count; index++)
        {
            BuildingStorey storey = building.Storeys[index];
            if (!before.Contains(storey) || storey == before[0])
            {
                OpenShaft(building, storey, place);
            }
        }
    }

    private static bool Climbs(BuildingFile building, SolidPart stairs)
    {
        float height = stairs.Place.Position.Y - building.Floor;
        int level = (int)MathF.Round(height / building.Size.Y);
        return building.Storeys.Exists(storey => storey.Level == level) && building.Storeys.Exists(storey => storey.Level == level + 1);
    }

    /// <summary>
    /// Adds a flight of stairs from the level's floor to the one above, with its foot at <paramref name="foot"/> across the floor
    /// and climbing toward +Z over <paramref name="run"/>, and opens the floor above over it. The new part's id, or null when the
    /// building has no storey above that level.
    /// </summary>
    public static string? AddStairs(BuildingFile building, int level, Vector2 foot, float run)
    {
        BuildingStorey? above = building.Storeys.Find(storey => storey.Level == level + 1);
        if (above is null || !building.Storeys.Exists(storey => storey.Level == level))
        {
            return null;
        }

        string id = building.AddStairs(level, foot, run);
        Vector3 low = new(foot.X - (StairsWidth / 2) - Clearance, 0, foot.Y - Clearance);
        // The opening ends where the top step does, so the step meets the floor above without a gap.
        Vector3 high = new(foot.X + (StairsWidth / 2) + Clearance, 0, foot.Y + run);
        OpenFloor(building, above, low, high);
        return id;
    }

    /// <summary>
    /// Gives the building an elevator at <paramref name="place"/> across the floor, resting on its lowest storey, and opens every
    /// floor above for its shaft.
    /// </summary>
    public static void AddElevator(BuildingFile building, Vector2 place)
    {
        building.SetElevator(place);
        for (int index = 1; index < building.Storeys.Count; index++)
        {
            OpenShaft(building, building.Storeys[index], place);
        }
    }

    private static void OpenShaft(BuildingFile building, BuildingStorey storey, Vector2 place)
    {
        float half = (PlatformSize / 2) + Clearance;
        Vector3 low = new(place.X - half, 0, place.Y - half);
        Vector3 high = new(place.X + half, 0, place.Y + half);
        OpenFloor(building, storey, low, high);
    }

    /// <summary>Cuts a box through the storey's floor between the two corners across it (X and Z in the building's space).</summary>
    private static void OpenFloor(BuildingFile building, BuildingStorey storey, Vector3 low, Vector3 high)
    {
        Vector3 size = new(high.X - low.X, building.Floor + (2 * Clearance), high.Z - low.Z);
        Vector3 foot = new((low.X + high.X) / 2, (storey.Level * building.Size.Y) - Clearance, (low.Z + high.Z) / 2);
        Cut(storey.Shell, size, foot);
    }

    /// <summary>Takes a box of the size, standing on <paramref name="foot"/> in the building's space, out of the part; nothing without Booleans.</summary>
    private static void Cut(SolidPart part, Vector3 size, Vector3 foot)
    {
        if (!SolidBoolean.IsAvailable)
        {
            return;
        }

        Matrix4x4 toBuilding = part.Place.ToMatrix();
        if (!Matrix4x4.Invert(toBuilding, out Matrix4x4 toPart))
        {
            return;
        }

        Matrix4x4 placed = Matrix4x4.CreateTranslation(foot) * toPart;
        SolidMesh box = SolidMesh.Box(size);
        SolidMesh cutter = box.Transform(placed);
        SolidMesh? opened = SolidBoolean.Combine(part.Mesh, cutter, BooleanOperation.Subtract);
        if (opened is not null)
        {
            part.Mesh = opened;
        }
    }
}
