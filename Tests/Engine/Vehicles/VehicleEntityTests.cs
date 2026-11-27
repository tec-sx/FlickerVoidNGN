using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
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
    public void APartWithALookIsDrawnAsItsLookAndTheRestAsGreyboxes()
    {
        _builder.WithVehicle(Van, VehicleFile.Create(VehicleType.Car)).Build();
        _builder.WriteLook(Van, ["body"], ["Mirror", "body"]);
        MeshSources meshes = _builder.Game.Services.GetService<MeshSources>()!;

        MadeMesh? body = meshes.Make($"{Van}#body", _builder.Content);
        MadeMesh? wheel = meshes.Make($"{Van}#wheel.1L", _builder.Content);

        body!.Model.Parts.Should().HaveCount(2, "the look's two objects of the body");
        body.OtherFiles.Should().Contain(VehicleLook.GetBuiltPath(Van), "a look built again draws again");
        wheel!.Model.Parts.Should().ContainSingle().Which.Nodes.Should().BeEmpty("it keeps its greybox");
        wheel.OtherFiles.Should().Contain(VehicleLook.GetBuiltPath(Van), "a look that gains the wheel draws it");
    }

    [Fact]
    public void ShowingGreyboxesDrawsEveryPartAsItsSolidAgain()
    {
        _builder.WithVehicle(Van, VehicleFile.Create(VehicleType.Car)).Build();
        _builder.WriteLook(Van, ["body"], ["Mirror", "body"]);
        Entity van = _builder.Place(Van);
        VehicleLooks looks = _builder.Game.Services.GetService<VehicleLooks>()!;
        MeshSources meshes = _builder.Game.Services.GetService<MeshSources>()!;
        World world = _builder.Game.World;

        looks.ShowGreyboxes = true;

        Entity part = _builder.Parts.Find(van, "body");
        string mesh = world.Get<MeshRenderer>(part).Mesh;
        mesh.Should().NotBe($"{Van}#body");
        meshes.Make(mesh, _builder.Content)!.Model.Parts.Should().ContainSingle("the greybox is one solid");
        looks.ShowGreyboxes = false;
        world.Get<MeshRenderer>(part).Mesh.Should().Be($"{Van}#body");
    }

    [Fact]
    public void TheLooksReportSaysWhatMatchedNothingAndWhatHasNoObject()
    {
        _builder.WithVehicle(Van, VehicleFile.Create(VehicleType.Car)).Build();
        _builder.WriteLook(Van, ["body"], ["Antenna.001"]);
        VehicleLooks looks = _builder.Game.Services.GetService<VehicleLooks>()!;

        VehicleLook? look = looks.Find(_builder.Content, Van);
        VehicleLook? none = looks.Find(_builder.Content, "Vans/Other.fvvehicle");

        look!.Unmatched.Should().Equal("Antenna.001");
        look.Greyboxed.Should().Equal("wheel.1L", "wheel.1R", "wheel.2L", "wheel.2R");
        none.Should().BeNull();
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

    [Fact]
    public void StandingPartsGetStaticTriangleBodiesAndADoorAKinematicHull()
    {
        VehicleFile car = VehicleFileTestsBuilder.WithDoor();
        _builder.WithVehicle(Van, car).Build();
        Entity van = _builder.Place(Van);
        World world = _builder.Game.World;

        Entity body = _builder.Parts.Find(van, "body");
        Entity door = _builder.Parts.Find(van, "door.1");

        TriangleCollider walls = world.Get<StaticBody>(body).Collider.Should().BeOfType<TriangleCollider>().Subject;
        walls.Indices.Should().HaveCount(36);
        world.Has<RigidBody>(body).Should().BeFalse();
        RigidBody moving = world.Get<RigidBody>(door);
        moving.Kinematic.Should().BeTrue();
        moving.Obstacle.Should().BeTrue();
        HullCollider hull = moving.Collider.Should().BeOfType<HullCollider>().Subject;
        hull.Points.Should().Equal(car.Doors[0].Mesh.Points);
        world.Has<StaticBody>(door).Should().BeFalse();
    }

    [Fact]
    public void ThePartsGetBodiesInTheSimulationWhenThereIsPhysics()
    {
        _builder.WithVehicle(Van, VehicleFileTestsBuilder.WithDoor()).Build();
        IPhysicsSystem physics = _builder.Game.AddPhysics();
        Entity van = _builder.Place(Van);

        _builder.Game.Tick(TimeSpan.FromSeconds(0.1));

        physics.HasBody(_builder.Parts.Find(van, "body")).Should().BeTrue();
        physics.HasBody(_builder.Parts.Find(van, "door.1")).Should().BeTrue();
    }
}
