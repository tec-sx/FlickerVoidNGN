using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// A <c>Name.fvbuilding</c> file: storeys from the bottom up (at most one cellar, level -1, then the ground floor, level 0, and the
/// floors above), a roof, stairs, an elevator and doors. Part ids: <c>cellar</c>, <c>storey.0</c>, <c>storey.1</c>, <c>roof</c>,
/// <c>stairs.1</c>, <c>elevator</c>, <c>door.1</c>. A file whose levels don't follow one another from a cellar or the ground floor
/// can't be read.
/// </summary>
public sealed class BuildingFile : ICreationFile, IJsonOnDeserialized
{
    public const string Extension = ".fvbuilding";

    public const string CellarId = "cellar";

    public const string RoofId = "roof";

    public const string ElevatorId = "elevator";

    /// <summary>The most storeys a building takes above the ground, the ground floor among them.</summary>
    public const int MaxStoreys = 12;

    private const float RoofThickness = 0.2f;
    private const float StairsWidth = 1.2f;
    private const float PlatformSize = 1.6f;
    private const float PlatformThickness = 0.1f;

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; set; }

    /// <summary>The size new storeys are made with: width, the height from one floor to the next, and depth.</summary>
    public Vector3 Size { get; set; } = new(8, 3, 6);

    /// <summary>How thick new storeys' walls are.</summary>
    public float Wall { get; set; } = 0.2f;

    /// <summary>How thick new storeys' floors are.</summary>
    public float Floor { get; set; } = 0.2f;

    /// <summary>The storeys, bottom first; <see cref="SetStoreys"/> changes how many.</summary>
    public List<BuildingStorey> Storeys { get; set; } = [];

    public required SolidPart Roof { get; set; }

    /// <summary>The flights of stairs, numbered from 1 in order: <c>stairs.1</c>.</summary>
    public List<SolidPart> Stairs { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BuildingElevator? Elevator { get; set; }

    /// <summary>The doors, numbered from 1 in order: <c>door.1</c>, <c>door.2</c>; the door tool adds them.</summary>
    public List<SolidPart> Doors { get; set; } = [];

    [JsonIgnore]
    public bool HasCellar => Storeys.Count > 0 && Storeys[0].Level < 0;

    /// <summary>How many storeys stand on the ground, the ground floor among them.</summary>
    [JsonIgnore]
    public int StoreysAbove => Storeys.Count(static storey => storey.Level >= 0);

    /// <summary>A new building: a ground floor under a roof.</summary>
    public static BuildingFile Create()
    {
        Vector3 size = new(8, 3, 6);
        SolidMesh slab = BuildingShapes.CreateSlab(size.X, RoofThickness, size.Z);
        SolidPart roof = BuildingShapes.CreatePart(slab, new Vector3(0, size.Y, 0));
        BuildingFile building = new() { Size = size, Roof = roof };
        building.Storeys.Add(building.CreateStorey(0));
        return building;
    }

    /// <summary>The id of the storey at the level: <c>cellar</c> below the ground, else <c>storey.0</c>, <c>storey.1</c> and up.</summary>
    public static string GetStoreyId(int level)
    {
        if (level < 0)
        {
            return CellarId;
        }

        return $"storey.{level}";
    }

    /// <summary>How high the top of the level's floor is above the building's origin.</summary>
    public float GetFloorHeight(int level) => (level * Size.Y) + Floor;

    /// <summary>
    /// Gives the building that many storeys on the ground and a cellar or none: storeys are added on top and taken off the top, the
    /// roof follows, and the storeys that stay are left as they were shaped.
    /// </summary>
    public void SetStoreys(int above, bool cellar)
    {
        if (above < 1 || above > MaxStoreys)
        {
            throw new ArgumentOutOfRangeException(nameof(above), above, $"A building takes 1 to {MaxStoreys} storeys on the ground.");
        }

        if (cellar && !HasCellar)
        {
            Storeys.Insert(0, CreateStorey(-1));
        }
        else if (!cellar && HasCellar)
        {
            Storeys.RemoveAt(0);
        }

        while (StoreysAbove > above)
        {
            Storeys.RemoveAt(Storeys.Count - 1);
        }

        while (StoreysAbove < above)
        {
            Storeys.Add(CreateStorey(Storeys[^1].Level + 1));
        }

        float top = (Storeys[^1].Level + 1) * Size.Y;
        Runtime.Components.Transform place = Roof.Place;
        place.Position = new Vector3(place.Position.X, top, place.Position.Z);
        Roof.Place = place;
    }

    /// <summary>
    /// Adds a flight of stairs from the level's floor up to the next one's, its foot at <paramref name="foot"/> across the floor
    /// (X and Z in the building's space) and climbing toward +Z over <paramref name="run"/>; its id. The hole in the floor above
    /// is the creator's to cut.
    /// </summary>
    public string AddStairs(int level, Vector2 foot, float run)
    {
        SolidMesh mesh = BuildingShapes.CreateStairs(StairsWidth, Size.Y, run);
        Vector3 position = new(foot.X, GetFloorHeight(level), foot.Y);
        Stairs.Add(BuildingShapes.CreatePart(mesh, position));
        return $"stairs.{Stairs.Count}";
    }

    /// <summary>
    /// Gives the building an elevator whose platform rests on the lowest storey's floor at <paramref name="place"/> across it (X
    /// and Z in the building's space), replacing one it had. The shaft through the floors above is the creator's to cut.
    /// </summary>
    public void SetElevator(Vector2 place)
    {
        SolidMesh mesh = BuildingShapes.CreateSlab(PlatformSize, PlatformThickness, PlatformSize);
        Vector3 position = new(place.X, GetFloorHeight(Storeys[0].Level), place.Y);
        Elevator = new BuildingElevator { Platform = BuildingShapes.CreatePart(mesh, position) };
    }

    /// <summary>How far above its rest the elevator's platform stands at each storey, bottom first.</summary>
    public float[] GetElevatorStops()
    {
        float[] stops = new float[Storeys.Count];
        for (int index = 0; index < stops.Length; index++)
        {
            stops[index] = (Storeys[index].Level - Storeys[0].Level) * Size.Y;
        }

        return stops;
    }

    /// <summary>Every part with its id: the storeys from the bottom, the roof, the stairs, the elevator's platform, then the doors.</summary>
    public IReadOnlyList<(string Id, SolidPart Part)> GetParts()
    {
        List<(string Id, SolidPart Part)> parts = [];
        foreach (BuildingStorey storey in Storeys)
        {
            parts.Add((GetStoreyId(storey.Level), storey.Shell));
        }

        parts.Add((RoofId, Roof));
        for (int index = 0; index < Stairs.Count; index++)
        {
            parts.Add(($"stairs.{index + 1}", Stairs[index]));
        }

        if (Elevator is { } elevator)
        {
            parts.Add((ElevatorId, elevator.Platform));
        }

        for (int index = 0; index < Doors.Count; index++)
        {
            parts.Add(($"door.{index + 1}", Doors[index]));
        }

        return parts;
    }

    void IJsonOnDeserialized.OnDeserialized()
    {
        if (Storeys.Count == 0)
        {
            throw new JsonException("A building needs a ground floor.");
        }

        int lowest = Storeys[0].Level;
        if (lowest is not (0 or -1))
        {
            throw new JsonException($"A building starts at its cellar (level -1) or its ground floor (level 0), not at level {lowest}.");
        }

        for (int index = 1; index < Storeys.Count; index++)
        {
            if (Storeys[index].Level != lowest + index)
            {
                throw new JsonException($"A building's storeys follow one another from the bottom, but level {Storeys[index].Level} follows level {Storeys[index - 1].Level}.");
            }
        }

        if (Storeys[^1].Level < 0)
        {
            throw new JsonException("A building needs a ground floor.");
        }
    }

    private BuildingStorey CreateStorey(int level)
    {
        SolidMesh tray = BuildingShapes.CreateTray(Size, Wall, Floor);
        SolidPart shell = BuildingShapes.CreatePart(tray, new Vector3(0, level * Size.Y, 0));
        return new BuildingStorey { Level = level, Shell = shell };
    }
}
