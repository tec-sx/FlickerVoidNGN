using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Events;

namespace FlickerVoid.Engine.Buildings.Tests;

public sealed class BuildingEntityTests : IDisposable
{
    private readonly BuildingEntityTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SpawnsTheFilesPartsUnderTheBuildingWithTheirMeshesAndExactCollision()
    {
        _builder.Write(BuildingFileTestsBuilder.WithEverything());

        Entity inn = _builder.Place();

        World world = _builder.World;
        Entity ground = _builder.Parts.Find(inn, "storey.0");
        world.Get<MeshRenderer>(ground).Mesh.Should().Be("Buildings/Inn.fvbuilding#storey.0");
        world.Get<StaticBody>(ground).Collider.Should().BeOfType<TriangleCollider>();
        _builder.Physics.HasBody(ground).Should().BeTrue();
        _builder.Parts.FindAll(inn).Should().HaveCount(6);
        Entity stairs = _builder.Parts.Find(inn, "stairs.1");
        world.Get<StaticBody>(stairs).Collider.Should().BeOfType<TriangleCollider>();
    }

    [Fact]
    public void EachStoreyIsAnIndoorVolumeAndTheCellarMutesTheOutside()
    {
        _builder.Write(BuildingFileTestsBuilder.WithEverything());

        Entity inn = _builder.Place();

        IndoorVolume ground = _builder.World.Get<IndoorVolume>(_builder.Parts.Find(inn, "storey.0"));
        ground.Size.Should().Be(new Vector3(7.6f, 3, 5.6f));
        ground.Ambient.Should().Be(0.1f);
        ground.OutsideMuted.Should().Be(0);
        IndoorVolume cellar = _builder.World.Get<IndoorVolume>(_builder.Parts.Find(inn, "cellar"));
        cellar.OutsideMuted.Should().BeGreaterThan(0);
        _builder.World.Has<IndoorVolume>(_builder.Parts.Find(inn, "roof")).Should().BeFalse();
    }

    [Fact]
    public void TheElevatorsPlatformIsUsedStandingOnItAndStopsAtEveryStorey()
    {
        _builder.Write(BuildingFileTestsBuilder.WithEverything());

        Entity inn = _builder.Place();

        World world = _builder.World;
        Entity platform = _builder.Parts.Find(inn, "elevator");
        world.Get<Elevator>(platform).Stops.Should().Equal(0, 3, 6);
        world.Get<UsePoint>(platform).StandOn.Should().BeTrue();
        world.Get<RigidBody>(platform).Kinematic.Should().BeTrue();
        world.Has<ActiveInteractable>(platform).Should().BeTrue("a part with a toggle is offered to interactors");
        Vector3 stand = FlickerVoid.Engine.Interaction.Interactors.InteractionSystem.GetUsePoint(world, platform);
        stand.Should().Be(new Vector3(2, -2.7f, 1));
    }

    [Fact]
    public void EachUseSendsThePlatformUpAStoreyAndFromTheTopBackToTheBottom()
    {
        _builder.Write(BuildingFileTestsBuilder.WithEverything());
        Entity inn = _builder.Place();
        Entity platform = _builder.Parts.Find(inn, "elevator");
        float rest = _builder.PlaceOf(platform).Position.Y;

        Use(platform, open: true);
        _builder.Run(3);
        float first = _builder.PlaceOf(platform).Position.Y;
        Use(platform, open: false);
        _builder.Run(3);
        float second = _builder.PlaceOf(platform).Position.Y;
        Use(platform, open: true);
        _builder.Run(5);

        first.Should().BeApproximately(rest + 3, 1e-3f);
        second.Should().BeApproximately(rest + 6, 1e-3f);
        _builder.PlaceOf(platform).Position.Y.Should().BeApproximately(rest, 1e-3f);
    }

    [Fact]
    public void ACharacterOnThePlatformRidesItUp()
    {
        BuildingFile building = BuildingFile.Create();
        building.SetStoreys(2, cellar: false);
        building.SetElevator(new Vector2(2, 1));
        _builder.Write(building);
        Entity inn = _builder.Place();
        Entity platform = _builder.Parts.Find(inn, "elevator");
        Entity hero = _builder.AddWalker(new Vector3(2, 0.3f, 1));
        _builder.Run(0.5f);

        Use(platform, open: true);
        _builder.Run(3);

        _builder.PlaceOf(hero).Position.Y.Should().BeApproximately(3.3f, 0.05f, "it stands on the platform at the upper floor");
    }

    [Fact]
    public void ADoorSpawnsWithItsJointAndIsReachedAtItsMiddle()
    {
        BuildingFile building = BuildingFile.Create();
        PartJoint joint = new() { Kind = JointKind.Hinge, Pivot = new Vector3(4, 0.2f, 0), Axis = Vector3.UnitY, Min = 0, Max = 90, Speed = 120 };
        SolidPart door = new()
        {
            Mesh = FlickerVoid.Engine.Runtime.Solids.SolidMesh.Box(new Vector3(0.2f, 2, 1)),
            Joint = joint,
            Toggle = new FlickerVoid.Engine.Gameplay.Tags.Tag("Interaction.Action.Toggle"),
        };
        building.Doors.Add(door);
        _builder.Write(building);

        Entity inn = _builder.Place();

        Entity part = _builder.Parts.Find(inn, "door.1");
        _builder.World.Get<RigidBody>(part).Collider.Should().BeOfType<HullCollider>();
        _builder.World.Get<UsePoint>(part).Offset.Should().Be(new Vector3(0, 1, 0));
        _builder.World.Has<Toggle>(part).Should().BeTrue();
    }

    [Fact]
    public void RespawnsThePartsWhenTheFileChangesKeepingThoseThatStay()
    {
        BuildingFile building = BuildingFile.Create();
        _builder.Write(building);
        Entity inn = _builder.Place();
        Entity ground = _builder.Parts.Find(inn, "storey.0");

        building.SetStoreys(2, cellar: false);
        _builder.Write(building);

        _builder.Parts.Find(inn, "storey.0").Should().Be(ground);
        _builder.Parts.Find(inn, "storey.1").Should().NotBe(Entity.Null);
        _builder.PlaceOf(_builder.Parts.Find(inn, "roof")).Position.Y.Should().Be(6);
    }

    private void Use(Entity platform, bool open)
    {
        EventBus events = _builder.Game.Events;
        events.Send(new ToggledEvent(platform, open, Entity.Null));
    }
}
