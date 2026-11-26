using System.Numerics;
using System.Text;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Tests.Scenes;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Solids;
using FlickerVoid.Engine.Vehicles;
using FlickerVoid.Engine.Vehicles.Serialization;

namespace FlickerVoid.Editor.Core.Tests.Creators;

/// <summary>
/// Opens a street with two vans of one car file, Van at the origin and Van 2 ten metres along X, and a crate greybox, under a
/// camera looking at the origin in a 640x480 viewport; the creator's file history and saves are counted here.
/// </summary>
internal sealed class CreatorModeTestsBuilder : IDisposable
{
    public const int Width = 640;
    public const int Height = 480;
    public const string File = "Vans/Van.fvvehicle";

    private const string Street = """
        { "entities": [
          { "name": "Van", "components": { "transform": {}, "vehicle": { "file": "Vans/Van.fvvehicle" } } },
          { "name": "Van 2", "components": { "transform": { "position": [10, 0, 0] }, "vehicle": { "file": "Vans/Van.fvvehicle" } } },
          { "name": "Crate", "components": { "transform": { "position": [-4, 0, 0] }, "greybox": { "size": [1, 1, 1] } } }
        ] }
        """;

    private readonly EditorSceneTestsBuilder _scene = new();
    private VehicleFile _car = VehicleFile.Create(VehicleType.Car);

    public CreatorModeTestsBuilder()
    {
        string json = Serialize(_car);
        _scene.WithFile(File, json).WithScene("Street.fvscene", Street).Open("Street.fvscene");
        Camera.LookAt(Vector3.Zero, yaw: 0.6f, pitch: -0.4f, distance: 8);
    }

    public EditorScene Scene => _scene.Scene;

    public World World => Scene.Game.World;

    public CreationParts Parts => Scene.Game.Services.GetService<CreationParts>()!;

    public EditorCamera Camera { get; } = new();

    public UndoHistory FileHistory { get; } = new();

    public int Saves { get; private set; }

    public Entity Find(string name) => Scene.Find(name)!.Value;

    public CreatorMode EnterInPlace(string name)
    {
        Entity placed = Find(name);
        return CreatorMode.InPlace(Scene, Camera, placed, FileHistory, Save, FindMesh);
    }

    public CreatorMode EnterAlone()
    {
        Vehicle vehicle = new() { File = File };
        return CreatorMode.Alone(Scene, Camera, vehicle, FileHistory, Save, FindMesh);
    }

    /// <summary>Writes the car file again with the wheel count, as the creator's panel would.</summary>
    public void WriteWheels(int count)
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        car.SetWheelCount(count);
        string json = Serialize(car);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Scene.Content.Write(File, bytes);
    }

    /// <summary>Writes a new car file with new meshes, as saving the file again does.</summary>
    public void ReplaceCar()
    {
        _car = VehicleFile.Create(VehicleType.Car);
        string json = Serialize(_car);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Scene.Content.Write(File, bytes);
    }

    /// <summary>The mesh of the car's part, as the creator's panel would give it.</summary>
    public SolidMesh? FindMesh(string id)
    {
        foreach ((string partId, VehiclePart part) in _car.GetParts())
        {
            if (partId == id)
            {
                return part.Mesh;
            }
        }

        return null;
    }

    public Vector2 PixelOf(Vector3 point) => Camera.Project(point, Width, Height);

    public void Dispose() => _scene.Dispose();

    private bool Save()
    {
        Saves++;
        return true;
    }

    private static string Serialize(VehicleFile vehicle) => JsonSerializer.Serialize(vehicle, VehiclesJsonContext.Default.VehicleFile);
}
