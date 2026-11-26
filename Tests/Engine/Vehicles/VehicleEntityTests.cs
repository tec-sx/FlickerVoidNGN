using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Vehicles.Tests;

public sealed class VehicleEntityTests : IDisposable
{
    private const string Van = "Vans/Van.fvvehicle";

    private readonly VehicleEntityTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SpawnsTheFilesPartsUnderTheVehicleWithTheirMeshes()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        _builder.WithVehicle(Van, car).Build();

        Entity van = _builder.Place(Van);

        IReadOnlyList<Entity> parts = _builder.Parts.FindAll(van);
        World world = _builder.Game.World;
        parts.Select(part => world.Get<CreationPart>(part).Id).Should().Equal("body", "wheel.1L", "wheel.1R", "wheel.2L", "wheel.2R");
        parts.Select(part => world.Get<MeshRenderer>(part).Mesh).Should().StartWith($"{Van}#body");
        _builder.Game.Hierarchy.Update(world);
        Vector3 wheel = world.Get<Transform>(parts[1]).Position;
        wheel.Should().Be(car.Wheels[0].Place.Position);
    }

    [Fact]
    public void MakesEachPartsMeshFromTheFile()
    {
        _builder.WithVehicle(Van, VehicleFile.Create(VehicleType.Car)).Build();
        MeshSources meshes = _builder.Game.Services.GetService<MeshSources>()!;

        MadeMesh? wheel = meshes.Make($"{Van}#wheel.1L", _builder.Content);
        MadeMesh? missing = meshes.Make($"{Van}#roof", _builder.Content);

        wheel.Should().NotBeNull();
        wheel!.File.Should().Be(Van);
        wheel.Model.Parts.Should().ContainSingle().Which.Indices.Should().NotBeEmpty();
        missing.Should().BeNull();
    }

    [Fact]
    public void RespawnsThePartsWhenTheFileChangesKeepingThoseThatStay()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        _builder.WithVehicle(Van, car).Build();
        Entity van = _builder.Place(Van);
        Entity body = _builder.Parts.Find(van, "body");

        car.SetWheelCount(6);
        _builder.Write(Van, car);

        _builder.Parts.FindAll(van).Should().HaveCount(7);
        _builder.Parts.Find(van, "body").Should().Be(body);
        _builder.Parts.Find(van, "wheel.3L").Should().NotBe(Entity.Null);
    }

    [Fact]
    public void KeepsThePartsWhileTheFileCantBeRead()
    {
        _builder.WithVehicle(Van, VehicleFile.Create(VehicleType.Car)).Build();
        Entity van = _builder.Place(Van);

        _builder.WriteText(Van, "{ \"type\": ");
        MadeMesh? body = _builder.Game.Services.GetService<MeshSources>()!.Make($"{Van}#body", _builder.Content);

        _builder.Parts.FindAll(van).Should().HaveCount(5);
        body.Should().NotBeNull();
    }

    [Fact]
    public void SpawnsNoPartsForAFileThatIsMissing()
    {
        _builder.Build();

        Entity van = _builder.Place("Vans/Gone.fvvehicle");

        _builder.Parts.FindAll(van).Should().BeEmpty();
    }

    [Fact]
    public void AddingVehiclesAgainKeepsOneOfEach()
    {
        _builder.WithVehicle(Van, VehicleFile.Create(VehicleType.Car)).Build();
        Game game = _builder.Game;

        game.AddVehicles();
        Entity van = _builder.Place(Van);

        _builder.Parts.FindAll(van).Should().HaveCount(5);
    }

    [Fact]
    public void ADoorPartSpawnsWithItsJointAndToggleAndOpensWhenToggled()
    {
        VehicleFile car = VehicleFileTestsBuilder.WithDoor();
        _builder.WithVehicle(Van, car).Build();
        Entity van = _builder.Place(Van);
        World world = _builder.Game.World;

        Entity door = _builder.Parts.Find(van, "door.1");

        world.Get<PartJoint>(door).Should().Be(car.Doors[0].Joint);
        world.Get<FlickerVoid.Engine.Interaction.Toggles.Toggle>(door).Open.Should().BeFalse();
        _builder.Game.Events.Send(new FlickerVoid.Engine.Interaction.Toggles.ToggledEvent(door, true, Entity.Null));
        _builder.Game.Events.Dispatch();
        for (int tick = 0; tick < 8; tick++)
        {
            _builder.Game.Tick(TimeSpan.FromSeconds(0.25));
        }

        world.Get<JointPose>(door).Position.Should().BeApproximately(90, 1e-3f);
    }
}
