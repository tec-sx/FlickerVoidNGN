using System.Numerics;
using System.Text;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Vehicles;
using FlickerVoid.Engine.Vehicles.Serialization;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class EditorSceneVehicleTests : IDisposable
{
    private const string Van = "Vans/Van.fvvehicle";
    private const string Street = """
        { "entities": [ { "name": "Van", "components": { "transform": {}, "vehicle": { "file": "Vans/Van.fvvehicle" } } } ] }
        """;

    private readonly EditorSceneTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SpawnsAndPicksThePartsOfTheScenesVehicles()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        string json = JsonSerializer.Serialize(car, VehiclesJsonContext.Default.VehicleFile);
        _builder.WithFile(Van, json).WithScene("Street.fvscene", Street).Open("Street.fvscene");
        EditorScene scene = _builder.Scene;
        Entity van = scene.Find("Van")!.Value;
        Ray down = new(new Vector3(0, 10, 0), -Vector3.UnitY);

        Entity? picked = scene.Picker.Pick(scene.Game.World, down);

        CreationParts parts = scene.Game.Services.GetService<CreationParts>()!;
        parts.FindAll(van).Should().HaveCount(5);
        picked.Should().Be(parts.Find(van, "body"));
    }

    [Fact]
    public void FollowsTheVehiclesFileInTheReloadedGame()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        string json = JsonSerializer.Serialize(car, VehiclesJsonContext.Default.VehicleFile);
        _builder.WithFile(Van, json).WithScene("Street.fvscene", Street).Open("Street.fvscene");
        EditorScene scene = _builder.Scene;
        scene.Reload([]);

        car.SetWheelCount(6);
        string changed = JsonSerializer.Serialize(car, VehiclesJsonContext.Default.VehicleFile);
        byte[] bytes = Encoding.UTF8.GetBytes(changed);
        scene.Content.Write(Van, bytes);

        Entity van = scene.Find("Van")!.Value;
        CreationParts parts = scene.Game.Services.GetService<CreationParts>()!;
        parts.FindAll(van).Should().HaveCount(7);
    }
}
