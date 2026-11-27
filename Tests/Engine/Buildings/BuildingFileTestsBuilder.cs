using System.Text.Json;

using FlickerVoid.Engine.Buildings.Serialization;

namespace FlickerVoid.Engine.Buildings.Tests;

/// <summary>Writes and reads building files as the content folder holds them.</summary>
internal static class BuildingFileTestsBuilder
{
    public static string Write(BuildingFile building) => JsonSerializer.Serialize(building, BuildingsJsonContext.Default.BuildingFile);

    public static BuildingFile Read(string json)
    {
        BuildingFile? building = JsonSerializer.Deserialize(json, BuildingsJsonContext.Default.BuildingFile);
        return building ?? throw new InvalidDataException("The file holds null.");
    }

    /// <summary>A building of two storeys over a cellar, with stairs from the ground floor and an elevator.</summary>
    public static BuildingFile WithEverything()
    {
        BuildingFile building = BuildingFile.Create();
        building.SetStoreys(2, cellar: true);
        building.AddStairs(0, new System.Numerics.Vector2(-3, -2), 4);
        building.SetElevator(new System.Numerics.Vector2(2, 1));
        return building;
    }
}
