using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// A <c>Name.fvbuilding</c> file: storeys from the bottom up (at most one cellar, level -1, then the ground floor, level 0, and the
/// floors above), each with its plan and the parts made from it, a roof, stairs, elevators and camera shots. Part ids: the storey's
/// volume <c>storey.0</c> (or <c>cellar</c>) and its parts <c>storey.0.floor</c>, <c>storey.0.walls</c>, <c>storey.0.wall.3</c>,
/// <c>storey.0.door.4</c>; <c>roof</c>; <c>stairs.1</c> and <c>stairs.1.railing</c>; <c>elevator.1</c>, its railing and
/// <c>elevator.1.call.0</c>; and doors hung by the door tool, <c>door.1</c>. A file whose levels don't follow one another from a
/// cellar or the ground floor can't be read.
/// </summary>
public sealed class BuildingFile : ICreationFile, IJsonOnDeserialized
{
    public const string Extension = ".fvbuilding";

    public const string CellarId = "cellar";

    public const string RoofId = "roof";

    /// <summary>The most storeys a building takes above the ground, the ground floor among them.</summary>
    public const int MaxStoreys = 12;

    /// <summary>The size of a call button's box: across, high and out of the wall.</summary>
    public static readonly Vector3 CallButtonSize = new(0.12f, 0.18f, 0.06f);

    /// <summary>How high a call button's foot stands above its floor.</summary>
    public const float CallButtonHeight = 1.1f;

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; set; }

    /// <summary>The size new buildings and storeys are made with: width, the height of a storey, and depth.</summary>
    public Vector3 Size { get; set; } = new(8, 3, 6);

    /// <summary>How thick new storeys' walls are.</summary>
    public float Wall { get; set; } = 0.3f;

    /// <summary>How thick new storeys' floors are.</summary>
    public float Floor { get; set; } = 0.2f;

    /// <summary>The storeys, bottom first.</summary>
    public List<BuildingStorey> Storeys { get; set; } = [];

    public BuildingRoof Roof { get; set; } = new();

    public List<BuildingStairs> Stairs { get; set; } = [];

    public List<BuildingElevator> Elevators { get; set; } = [];

    public List<BuildingShot> Shots { get; set; } = [];

    /// <summary>Doors hung by the shared door tool, numbered from 1 in order: <c>door.1</c>; a building's own openings are in its storeys.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<SolidPart> Doors { get; set; } = [];

    [JsonIgnore]
    public bool HasCellar => Storeys.Count > 0 && Storeys[0].Level < 0;

    /// <summary>How many storeys stand on the ground, the ground floor among them.</summary>
    [JsonIgnore]
    public int StoreysAbove => Storeys.Count(static storey => storey.Level >= 0);

    /// <summary>A new building: a ground floor of one room with a front door, under a flat roof; the creator makes its parts.</summary>
    public static BuildingFile Create()
    {
        BuildingFile building = new();
        BuildingStorey ground = building.CreateStorey(0);
        building.Storeys.Add(ground);
        PlanWall front = ground.Walls[0];
        List<Vector2> path = PlanGeometry.GetPath(ground, front);
        float length = PlanGeometry.GetLength(path);
        PlanOpening door = new() { Id = ground.NextId(), Name = "Front door", Kind = OpeningKind.Door, Wall = front.Id, Along = length / 2, Width = 1.1f, Height = 2.2f };
        ground.Openings.Add(door);
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

    public BuildingStorey? FindStorey(int level) => Storeys.Find(storey => storey.Level == level);

    /// <summary>How high the underside of the level's floor is above the building's origin: the ground floor's at 0.</summary>
    public float GetBase(int level)
    {
        float height = 0;
        if (level < 0)
        {
            foreach (BuildingStorey storey in Storeys)
            {
                if (storey.Level >= level && storey.Level < 0)
                {
                    height -= storey.Height;
                }
            }

            return height;
        }

        foreach (BuildingStorey storey in Storeys)
        {
            if (storey.Level >= 0 && storey.Level < level)
            {
                height += storey.Height;
            }
        }

        if (FindStorey(level) is null && level > 0)
        {
            height += Size.Y * (level - Storeys.Count(static storey => storey.Level >= 0));
        }

        return height;
    }

    /// <summary>How high the top of the level's floor is above the building's origin.</summary>
    public float GetFloorHeight(int level)
    {
        float floor = FindStorey(level)?.Floor ?? Floor;
        return GetBase(level) + floor;
    }

    /// <summary>How high the top of the building is: the top storey's ceiling, where the roof stands.</summary>
    public float GetTop()
    {
        if (Storeys.Count == 0)
        {
            return 0;
        }

        BuildingStorey top = Storeys[^1];
        return GetBase(top.Level) + top.Height;
    }

    /// <summary>A new storey at the level with the file's sizes: four outside walls round one room.</summary>
    public BuildingStorey CreateStorey(int level)
    {
        BuildingStorey storey = new() { Level = level, Height = Size.Y, Wall = Wall, Floor = Floor };
        float halfX = (Size.X - Wall) / 2;
        float halfZ = (Size.Z - Wall) / 2;

        // Round to the left seen from above, so each outside wall's right is outside: front (toward -Z) first, from right to left.
        Vector2[] corners = [new(halfX, -halfZ), new(-halfX, -halfZ), new(-halfX, halfZ), new(halfX, halfZ)];
        for (int index = 0; index < corners.Length; index++)
        {
            storey.Corners.Add(new PlanCorner { Id = index + 1, X = corners[index].X, Z = corners[index].Y });
        }

        for (int index = 0; index < corners.Length; index++)
        {
            int next = (index + 1) % corners.Length;
            storey.Walls.Add(new PlanWall { Id = corners.Length + index + 1, From = index + 1, To = next + 1, Outside = true });
        }

        string name = level < 0 ? "Cellar" : "Hall";
        if (level > 0)
        {
            name = "Landing";
        }

        storey.Rooms.Add(new PlanRoom { Name = name, X = 0, Z = 0 });
        return storey;
    }

    /// <summary>How high above its rest an elevator's platform stands at each storey it stops at, bottom first, with their levels.</summary>
    public (int Level, float Height)[] GetElevatorStops(BuildingElevator elevator)
    {
        List<(int, float)> stops = [];
        float rest = GetFloorHeight(elevator.From);
        foreach (BuildingStorey storey in Storeys)
        {
            if (storey.Level >= elevator.From && storey.Level <= elevator.To)
            {
                stops.Add((storey.Level, GetFloorHeight(storey.Level) - rest));
            }
        }

        return [.. stops];
    }

    /// <summary>Where an elevator's call button stands on a level: beside the shaft on the side it is stepped onto, in the building's space.</summary>
    public Vector3 GetCallButton(BuildingElevator elevator, int level)
    {
        float turn = float.DegreesToRadians(elevator.Heading);
        Vector2 front = new(MathF.Sin(turn), -MathF.Cos(turn));
        Vector2 across = new(-front.Y, front.X);
        float half = elevator.Size / 2;
        Vector2 place = new Vector2(elevator.X, elevator.Z) + (front * (half + 0.15f)) + (across * (half + 0.25f));
        return new Vector3(place.X, GetFloorHeight(level) + CallButtonHeight, place.Y);
    }

    public static string GetElevatorId(BuildingElevator elevator) => $"elevator.{elevator.Id}";

    public static string GetCallId(BuildingElevator elevator, int level) => $"elevator.{elevator.Id}.call.{level}";

    public static string GetStairsId(BuildingStairs stairs) => $"stairs.{stairs.Id}";

    public static string GetShotId(BuildingShot shot) => $"shot.{shot.Id}";

    /// <summary>The id of a part a storey's plan made, from its id within the storey.</summary>
    public static string GetPartId(BuildingStorey storey, string local) => $"{GetStoreyId(storey.Level)}.{local}";

    /// <summary>
    /// Every solid part with its id: each storey's (an old storey's shell, or the parts made from its plan), the roof, the stairs and
    /// their railings, the elevators' platforms, railings and call buttons, then the doors the door tool hung.
    /// </summary>
    public IReadOnlyList<(string Id, SolidPart Part)> GetParts()
    {
        List<(string Id, SolidPart Part)> parts = [];
        foreach (BuildingStorey storey in Storeys)
        {
            if (storey.Shell is { } shell)
            {
                parts.Add((GetStoreyId(storey.Level), shell));
            }

            foreach ((string local, SolidPart part) in storey.Parts)
            {
                parts.Add((GetPartId(storey, local), part));
            }
        }

        if (Roof.Part is { } roof && !Roof.None)
        {
            parts.Add((RoofId, roof));
        }

        foreach (BuildingStairs stairs in Stairs)
        {
            if (stairs.Part is { } steps)
            {
                parts.Add((GetStairsId(stairs), steps));
            }

            if (stairs.RailingPart is { } railing)
            {
                parts.Add(($"{GetStairsId(stairs)}.railing", railing));
            }
        }

        foreach (BuildingElevator elevator in Elevators)
        {
            if (elevator.Platform is { } platform)
            {
                parts.Add((GetElevatorId(elevator), platform));
            }

            if (elevator.RailingPart is { } railing)
            {
                parts.Add(($"{GetElevatorId(elevator)}.railing", railing));
            }

            if (!elevator.CallButtons)
            {
                continue;
            }

            foreach ((int level, _) in GetElevatorStops(elevator))
            {
                parts.Add((GetCallId(elevator, level), MakeCallButton(elevator, level)));
            }
        }

        for (int index = 0; index < Doors.Count; index++)
        {
            parts.Add(($"door.{index + 1}", Doors[index]));
        }

        return parts;
    }

    /// <summary>A call button's small box, standing on its place and turned to face out of the shaft's side.</summary>
    private SolidPart MakeCallButton(BuildingElevator elevator, int level)
    {
        Vector3 place = GetCallButton(elevator, level);
        Runtime.Components.Transform transform = new()
        {
            Position = place,
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -float.DegreesToRadians(elevator.Heading)),
        };
        return new SolidPart { Place = transform, Mesh = Runtime.Solids.SolidMesh.Box(CallButtonSize) };
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

        // A storey made before plans gets the plan of a new storey, so the creator can make its parts; its shell stays until then.
        for (int index = 0; index < Storeys.Count; index++)
        {
            BuildingStorey storey = Storeys[index];
            if (storey.Corners.Count > 0 || storey.Shell is null)
            {
                continue;
            }

            BuildingStorey planned = CreateStorey(storey.Level);
            storey.Corners = planned.Corners;
            storey.Walls = planned.Walls;
            storey.Rooms = planned.Rooms;
        }
    }
}
