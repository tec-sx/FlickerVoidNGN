using System.Text.Json;
using System.Text.Json.Nodes;

using FlickerVoid.Engine.Vehicles.Serialization;

namespace FlickerVoid.Engine.Vehicles.Tests;

/// <summary>Writes and reads vehicle files as the content folder holds them.</summary>
internal static class VehicleFileTestsBuilder
{
    public static string Write(VehicleFile vehicle) => JsonSerializer.Serialize(vehicle, VehiclesJsonContext.Default.VehicleFile);

    public static VehicleFile Read(string json)
    {
        VehicleFile? vehicle = JsonSerializer.Deserialize(json, VehiclesJsonContext.Default.VehicleFile);
        return vehicle ?? throw new InvalidDataException("The file holds null.");
    }

    /// <summary>A new car's file with the body's last face left out, so its mesh is open.</summary>
    public static string WithOpenBody()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        string json = Write(car);
        JsonNode file = JsonNode.Parse(json)!;
        JsonArray faces = file["body"]!["mesh"]!["faces"]!.AsArray();
        faces.RemoveAt(faces.Count - 1);
        return file.ToJsonString();
    }
}
