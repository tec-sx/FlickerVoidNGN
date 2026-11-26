using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Solids;
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

    /// <summary>A new car with a door on its left side, hinged at its front edge, that opens outward 90 degrees.</summary>
    public static VehicleFile WithDoor()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        PartJoint joint = new() { Kind = JointKind.Hinge, Pivot = new Vector3(1, 0.5f, 1), Axis = Vector3.UnitY, Min = 0, Max = 90, Speed = 120 };
        VehiclePart door = new() { Mesh = SolidMesh.Box(new Vector3(0.1f, 1, 1)), Joint = joint, Toggle = new Tag("Interaction.Action.Toggle") };
        car.Doors.Add(door);
        return car;
    }

    /// <summary>The box round the body in the vehicle's space, for a body that is neither turned nor scaled.</summary>
    public static (Vector3 Low, Vector3 High) GetBodyBox(VehicleFile vehicle)
    {
        Vector3 place = vehicle.Body.Place.Position;
        Vector3 low = new(float.MaxValue);
        Vector3 high = new(float.MinValue);
        foreach (Vector3 point in vehicle.Body.Mesh.Points)
        {
            low = Vector3.Min(low, point + place);
            high = Vector3.Max(high, point + place);
        }

        return (low, high);
    }

    /// <summary>How far the body reaches behind the last axle.</summary>
    public static float GetRearOverhang(VehicleFile vehicle)
    {
        (Vector3 low, Vector3 _) = GetBodyBox(vehicle);
        return vehicle.Wheels[^1].Place.Position.Z - low.Z;
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
