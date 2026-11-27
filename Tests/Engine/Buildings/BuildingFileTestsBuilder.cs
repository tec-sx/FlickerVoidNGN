using System.Numerics;
using System.Text.Json;

using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Buildings.Serialization;

namespace FlickerVoid.Engine.Buildings.Tests;

/// <summary>Writes and reads building files as the content folder holds them, and makes buildings with their parts made from their plans.</summary>
internal static class BuildingFileTestsBuilder
{
    public static string Write(BuildingFile building) => JsonSerializer.Serialize(building, BuildingsJsonContext.Default.BuildingFile);

    public static BuildingFile Read(string json)
    {
        BuildingFile? building = JsonSerializer.Deserialize(json, BuildingsJsonContext.Default.BuildingFile);
        return building ?? throw new InvalidDataException("The file holds null.");
    }

    /// <summary>A new building with that many storeys on the ground and a cellar or none, its parts made.</summary>
    public static BuildingFile WithStoreys(int above, bool cellar)
    {
        BuildingFile building = BuildingFile.Create();
        for (int level = 1; level < above; level++)
        {
            building.Storeys.Add(building.CreateStorey(level));
        }

        if (cellar)
        {
            building.Storeys.Insert(0, building.CreateStorey(-1));
        }

        BuildingMaker.Make(building, null);
        return building;
    }

    /// <summary>A building of two storeys over a cellar, with stairs from the ground floor and an elevator from the cellar to the top.</summary>
    public static BuildingFile WithEverything()
    {
        BuildingFile building = WithStoreys(2, cellar: true);
        building.Stairs.Add(new BuildingStairs { Id = 1, Level = 0, X = -3, Z = -2 });
        building.Elevators.Add(new BuildingElevator { Id = 1, X = 2, Z = 1, From = -1, To = 1 });
        BuildingMaker.Make(building, null);
        return building;
    }

    /// <summary>The ground floor's front door, as a new building has it.</summary>
    public static PlanOpening FrontDoor(BuildingFile building) => building.Storeys.Single(storey => storey.Level == 0).Openings[0];

    /// <summary>A shot of the ground floor's room, switched to on entering it.</summary>
    public static BuildingShot HallShot(TriggerMoment when)
    {
        ShotTrigger trigger = new() { Target = TriggerTarget.Room, Level = 0, Room = "Hall", When = when, Change = TriggerChange.Cut };
        BuildingShot shot = new() { Id = 1, Name = "Hall shot", Position = new Vector3(0, 6, -6), Triggers = [trigger] };
        return shot;
    }
}
