using System.Numerics;
using System.Text;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Components;
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
    public void SpawnsTheScenesVehiclesAndPicksEachByAnyOfItsParts()
    {
        EditorScene scene = OpenStreet();
        Entity van = scene.Find("Van")!.Value;
        CreationParts parts = scene.Game.Services.GetService<CreationParts>()!;
        Vector3 wheel = scene.Game.World.Get<Transform>(parts.Find(van, "wheel.1L")).Position;
        Ray down = new(new Vector3(0, 10, 0), -Vector3.UnitY);
        Ray side = new(wheel + new Vector3(10, 0, 0), -Vector3.UnitX);

        Entity? pickedBody = scene.Picker.Pick(scene.Game.World, down);
        Entity? pickedWheel = scene.Picker.Pick(scene.Game.World, side);

        parts.FindAll(van).Should().HaveCount(5);
        pickedBody.Should().Be(van);
        pickedWheel.Should().Be(van);
    }

    [Fact]
    public void BoundsAVehicleByItsPartsInItsOwnSpace()
    {
        EditorScene scene = OpenStreet();
        Entity van = scene.Find("Van")!.Value;
        Transform moved = new() { Position = new Vector3(5, 0, 2), Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f) };
        scene.SetComponent(van, moved);
        (Vector3 min, Vector3 max) = GetPartsBox(VehicleFile.Create(VehicleType.Car));

        bool found = scene.Picker.TryGetBounds(scene.Game.World, van, out Vector3 low, out Vector3 high);

        found.Should().BeTrue();
        Vector3.Distance(low, min).Should().BeLessThan(1e-3f);
        Vector3.Distance(high, max).Should().BeLessThan(1e-3f);
    }

    [Fact]
    public void MovesAVehiclesPartsWithIt()
    {
        EditorScene scene = OpenStreet();
        Entity van = scene.Find("Van")!.Value;
        CreationParts parts = scene.Game.Services.GetService<CreationParts>()!;
        Entity body = parts.Find(van, "body");
        Vector3 before = scene.Game.World.Get<Transform>(body).Position;

        scene.SetComponent(van, new Transform { Position = new Vector3(3, 0, 0) });

        Vector3 after = scene.Game.World.Get<Transform>(body).Position;
        Vector3.Distance(after, before + new Vector3(3, 0, 0)).Should().BeLessThan(1e-4f);
    }

    [Fact]
    public void DuplicatesAVehicleWithPartsOfItsOwn()
    {
        EditorScene scene = OpenStreet();
        Entity van = scene.Find("Van")!.Value;

        Entity copy = scene.DuplicateEntity(van);

        CreationParts parts = scene.Game.Services.GetService<CreationParts>()!;
        parts.FindAll(copy).Should().HaveCount(5);
        parts.FindAll(van).Should().HaveCount(5);
        parts.FindAll(copy).Should().NotIntersectWith(parts.FindAll(van));
        scene.Entities.Should().HaveCount(2);
    }

    [Fact]
    public void DeletesAVehicleWithItsPartsAndUndoBringsThemBack()
    {
        EditorScene scene = OpenStreet();
        Entity van = scene.Find("Van")!.Value;
        CreationParts parts = scene.Game.Services.GetService<CreationParts>()!;
        IReadOnlyList<Entity> spawned = parts.FindAll(van);

        scene.DestroyEntity(van);
        bool partsGone = spawned.All(part => !scene.Game.World.IsAlive(part));
        scene.History.Undo();

        partsGone.Should().BeTrue();
        Entity restored = scene.Find("Van")!.Value;
        parts.FindAll(restored).Should().HaveCount(5);
    }

    [Fact]
    public void HidesAndShowsAVehiclesPartsWithIt()
    {
        EditorScene scene = OpenStreet();
        Entity van = scene.Find("Van")!.Value;
        CreationParts parts = scene.Game.Services.GetService<CreationParts>()!;
        Ray down = new(new Vector3(0, 10, 0), -Vector3.UnitY);

        scene.SetHidden([van], hidden: true);
        bool hidden = parts.FindAll(van).All(scene.IsHidden);
        Entity? pickedHidden = scene.Picker.Pick(scene.Game.World, down);
        scene.ShowAll();

        hidden.Should().BeTrue();
        pickedHidden.Should().BeNull();
        parts.FindAll(van).Should().OnlyContain(part => !scene.IsHidden(part));
    }

    [Fact]
    public void FollowsTheVehiclesFileInTheReloadedGame()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        EditorScene scene = OpenStreet();
        scene.Reload([]);

        car.SetWheelCount(6);
        string changed = JsonSerializer.Serialize(car, VehiclesJsonContext.Default.VehicleFile);
        byte[] bytes = Encoding.UTF8.GetBytes(changed);
        scene.Content.Write(Van, bytes);

        Entity van = scene.Find("Van")!.Value;
        CreationParts parts = scene.Game.Services.GetService<CreationParts>()!;
        parts.FindAll(van).Should().HaveCount(7);
    }

    /// <summary>Opens Street.fvscene, holding one Van whose file is a new car.</summary>
    private EditorScene OpenStreet()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        string json = JsonSerializer.Serialize(car, VehiclesJsonContext.Default.VehicleFile);
        _builder.WithFile(Van, json).WithScene("Street.fvscene", Street).Open("Street.fvscene");
        return _builder.Scene;
    }

    /// <summary>The box round every part's mesh in its place, in the vehicle's space.</summary>
    private static (Vector3 Min, Vector3 Max) GetPartsBox(VehicleFile vehicle)
    {
        Vector3 min = new(float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity);
        foreach ((string _, VehiclePart part) in vehicle.GetParts())
        {
            Matrix4x4 place = part.Place.ToMatrix();
            foreach (Vector3 point in part.Mesh.Points)
            {
                Vector3 placed = Vector3.Transform(point, place);
                min = Vector3.Min(min, placed);
                max = Vector3.Max(max, placed);
            }
        }

        return (min, max);
    }
}
