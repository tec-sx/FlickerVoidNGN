using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Cameras.Triggers;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Buildings.Tests;

public sealed class BuildingEntityTests : IDisposable
{
    private static readonly Tag Toggle = new("Interaction.Action.Toggle");

    private readonly BuildingEntityTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SpawnsThePartsThePlansMadeWithTheirMeshesAndExactCollision()
    {
        _builder.Write(BuildingFileTestsBuilder.WithEverything());

        Entity inn = _builder.Place();

        World world = _builder.World;
        Entity walls = _builder.Parts.Find(inn, "storey.0.walls");
        world.Get<MeshRenderer>(walls).Mesh.Should().Be("Buildings/Inn.fvbuilding#storey.0.walls");
        world.Get<StaticBody>(walls).Collider.Should().BeOfType<TriangleCollider>();
        _builder.Physics.HasBody(walls).Should().BeTrue();
        world.Get<StaticBody>(_builder.Parts.Find(inn, "stairs.1")).Collider.Should().BeOfType<TriangleCollider>();
        world.Has<StaticBody>(_builder.Parts.Find(inn, "storey.1.floor")).Should().BeTrue();
    }

    [Fact]
    public void EachStoreyIsAnIndoorVolumeInsideItsWallsAndTheCellarMutesTheOutside()
    {
        _builder.Write(BuildingFileTestsBuilder.WithEverything());

        Entity inn = _builder.Place();

        Entity groundPart = _builder.Parts.Find(inn, "storey.0");
        IndoorVolume ground = _builder.World.Get<IndoorVolume>(groundPart);
        ground.Size.X.Should().BeApproximately(7.4f, 1e-4f);
        ground.Size.Y.Should().BeApproximately(2.8f, 1e-4f);
        ground.Size.Z.Should().BeApproximately(5.4f, 1e-4f);
        _builder.PlaceOf(groundPart).Position.Y.Should().BeApproximately(0.2f, 1e-4f);
        ground.OutsideMuted.Should().Be(0);
        IndoorVolume cellar = _builder.World.Get<IndoorVolume>(_builder.Parts.Find(inn, "cellar"));
        cellar.OutsideMuted.Should().BeGreaterThan(0);
        _builder.World.Has<IndoorVolume>(_builder.Parts.Find(inn, "roof")).Should().BeFalse();
    }

    [Fact]
    public void AStoreysFloorIsADarkerGreyThanItsWallsSoAPlanReadsFromAbove()
    {
        BuildingFile building = BuildingFileTestsBuilder.WithStoreys(1, cellar: false);
        BuildingMaker.Make(building, null);
        _builder.Write(building);
        MeshSources meshes = _builder.Game.Services.GetService<MeshSources>()!;

        MadeMesh floor = meshes.Make($"{BuildingEntityTestsBuilder.File}#storey.0.floor", _builder.Content)!;
        MadeMesh walls = meshes.Make($"{BuildingEntityTestsBuilder.File}#storey.0.walls", _builder.Content)!;

        float floorGrey = floor.Model.Parts[0].Material.BaseColor.X;
        float wallGrey = walls.Model.Parts[0].Material.BaseColor.X;
        wallGrey.Should().Be(SolidModel.Color.X);
        floorGrey.Should().BeLessThan(wallGrey * 0.8f, "seen from above a wall's top and the floor face the same way");
    }

    [Fact]
    public void EveryRoomOfAStoreyWithMoreThanOneIsAnIndoorVolumeOfItsOwn()
    {
        BuildingFile building = BuildingFileTestsBuilder.WithStoreys(1, cellar: false);
        BuildingStorey divided = BuildingPlanTestsBuilder.WithDividingWall();
        divided.Rooms.Add(new PlanRoom { Name = "Store", X = 3, Z = 0, Ambient = 0.3f });
        building.Storeys[0] = divided;
        BuildingMaker.Make(building, null);
        _builder.Write(building);

        Entity inn = _builder.Place();

        Entity first = _builder.Parts.Find(inn, "storey.0.room.1");
        Entity second = _builder.Parts.Find(inn, "storey.0.room.2");
        first.Should().NotBe(Entity.Null);
        second.Should().NotBe(Entity.Null);
        float[] ambients = [_builder.World.Get<IndoorVolume>(first).Ambient, _builder.World.Get<IndoorVolume>(second).Ambient];
        ambients.Should().Contain(0.3f);
    }

    [Fact]
    public void ADoorSwingsOnItsHingeWhenUsedAndIsReachedAtItsHandle()
    {
        BuildingFile building = BuildingFileTestsBuilder.WithStoreys(1, cellar: false);
        _builder.Write(building);
        Entity inn = _builder.Place();
        Entity door = _builder.Parts.Find(inn, "storey.0.door.9");
        Quaternion shut = _builder.PlaceOf(door).Rotation;
        Vector3 reach = InteractionSystem.GetUsePoint(_builder.World, door);

        _builder.Game.Events.Send(new InteractionActionEvent(Entity.Null, door, Toggle));
        _builder.Run(1);

        _builder.World.Get<RigidBody>(door).Collider.Should().BeOfType<HullCollider>();
        _builder.World.Has<ActiveInteractable>(door).Should().BeTrue("a part with a toggle is offered to interactors");
        _builder.World.Get<Toggle>(door).Open.Should().BeTrue();
        _builder.PlaceOf(door).Rotation.Should().NotBe(shut);
        reach.Z.Should().BeApproximately(-2.85f, 0.2f, "a shut door is reached in the front wall");
        MathF.Abs(reach.X).Should().BeApproximately(0.45f, 0.01f, "at its handle, a hand in from the leaf's free edge");
        reach.Y.Should().BeApproximately(1.2f, 0.01f, "a metre above the floor");
    }

    [Fact]
    public void ALockedDoorStaysShutUntilUnlockedAndCarriesItsKey()
    {
        BuildingFile building = BuildingFile.Create();
        PlanOpening front = BuildingFileTestsBuilder.FrontDoor(building);
        front.Locked = true;
        front.Key = new Tag("Key.Cellar");
        front.Pickable = false;
        BuildingMaker.Make(building, null);
        _builder.Write(building);
        Entity inn = _builder.Place();
        Entity door = _builder.Parts.Find(inn, "storey.0.door.9");

        _builder.Game.Events.Send(new InteractionActionEvent(Entity.Null, door, Toggle));
        _builder.Run(0.2f);
        bool openedLocked = _builder.World.Get<Toggle>(door).Open;
        ref Lockable doorLock = ref _builder.World.Get<Lockable>(door);
        doorLock.Locked = false;
        _builder.Game.Events.Send(new InteractionActionEvent(Entity.Null, door, Toggle));
        _builder.Run(0.2f);

        openedLocked.Should().BeFalse();
        _builder.World.Get<Lockable>(door).Key.Should().Be(new Tag("Key.Cellar"));
        _builder.World.Get<Lockable>(door).Pickable.Should().BeFalse();
        _builder.World.Get<Toggle>(door).Open.Should().BeTrue();
    }

    [Fact]
    public void TheElevatorsPlatformIsRiddenStandingOnItAndStopsAtEveryStorey()
    {
        _builder.Write(BuildingFileTestsBuilder.WithEverything());

        Entity inn = _builder.Place();

        World world = _builder.World;
        Entity platform = _builder.Parts.Find(inn, "elevator.1");
        world.Get<Elevator>(platform).Stops.Should().Equal(0, 3, 6);
        world.Get<Elevator>(platform).Levels.Should().Equal(-1, 0, 1);
        world.Get<UsePoint>(platform).StandOn.Should().BeTrue();
        world.Get<RigidBody>(platform).Kinematic.Should().BeTrue();
        world.Has<ActiveInteractable>(platform).Should().BeTrue();
        Vector3 stand = InteractionSystem.GetUsePoint(world, platform);
        stand.Should().Be(new Vector3(2, -2.7f, 1));
    }

    [Fact]
    public void RidingTakesThePlatformAStoreyOnTheWayItGoesTurningAtTheTopAndTheBottom()
    {
        _builder.Write(BuildingFileTestsBuilder.WithEverything());
        Entity inn = _builder.Place();
        Entity platform = _builder.Parts.Find(inn, "elevator.1");
        float rest = _builder.PlaceOf(platform).Position.Y;
        List<float> heights = [];

        for (int ride = 0; ride < 4; ride++)
        {
            _builder.Game.Events.Send(new InteractionActionEvent(Entity.Null, platform, Toggle));
            _builder.Run(3);
            heights.Add(_builder.PlaceOf(platform).Position.Y - rest);
        }

        heights.Select(height => MathF.Round(height, 2)).Should().Equal(3, 6, 3, 0);
    }

    [Fact]
    public void ACallButtonBringsThePlatformToItsStorey()
    {
        _builder.Write(BuildingFileTestsBuilder.WithEverything());
        Entity inn = _builder.Place();
        Entity platform = _builder.Parts.Find(inn, "elevator.1");
        Entity top = _builder.Parts.Find(inn, "elevator.1.call.1");
        float rest = _builder.PlaceOf(platform).Position.Y;

        _builder.Game.Events.Send(new InteractionActionEvent(Entity.Null, top, Toggle));
        _builder.Run(5);

        _builder.World.Has<ActiveInteractable>(top).Should().BeTrue("a call button is used like a door");
        _builder.PlaceOf(top).Position.Y.Should().BeApproximately(3.2f + BuildingFile.CallButtonHeight, 1e-4f);
        _builder.PlaceOf(platform).Position.Y.Should().BeApproximately(rest + 6, 1e-3f);
    }

    [Fact]
    public void ACharacterOnThePlatformRidesItUp()
    {
        BuildingFile building = BuildingFileTestsBuilder.WithStoreys(2, cellar: false);
        building.Elevators.Add(new BuildingElevator { Id = 1, X = 2, Z = 1, From = 0, To = 1 });
        BuildingMaker.Make(building, null);
        _builder.Write(building);
        Entity inn = _builder.Place();
        Entity platform = _builder.Parts.Find(inn, "elevator.1");
        Entity hero = _builder.AddWalker(new Vector3(2, 0.4f, 1));
        _builder.Run(0.5f);

        _builder.Game.Events.Send(new InteractionActionEvent(Entity.Null, platform, Toggle));
        _builder.Run(3);

        _builder.PlaceOf(hero).Position.Y.Should().BeApproximately(3.3f, 0.05f, "it stands on the platform at the upper floor");
    }

    [Fact]
    public void AShotIsAnOrthographicCameraTheSceneNeverStartsOn()
    {
        BuildingFile building = BuildingFileTestsBuilder.WithStoreys(1, cellar: false);
        building.Shots.Add(BuildingFileTestsBuilder.HallShot(TriggerMoment.Enter));
        _builder.Write(building);
        Entity street = _builder.AddStreetShot();

        Entity inn = _builder.Place();

        Entity shot = _builder.Parts.Find(inn, "shot.1");
        _builder.World.Has<Shot>(shot).Should().BeTrue();
        _builder.World.Get<PlacedShot>(shot).Rig.Layers.Should().HaveCount(2, "it follows and looks");
        _builder.World.Has<TriggerArea>(_builder.Parts.Find(inn, "shot.1.trigger.1")).Should().BeTrue();
        _builder.Director.Live.Should().Be(street);
    }

    [Fact]
    public void WalkingIntoTheRoomSwitchesToItsShotAndLeavingGoesBack()
    {
        BuildingFile building = BuildingFileTestsBuilder.WithStoreys(1, cellar: false);
        building.Shots.Add(BuildingFileTestsBuilder.HallShot(TriggerMoment.Both));
        building.Shots[0].Triggers[0].Return = TriggerReturn.Previous;
        _builder.Write(building);
        Entity street = _builder.AddStreetShot();
        Entity inn = _builder.Place();
        Entity hero = _builder.AddWalker(new Vector3(0, 0, -8));
        _builder.Run(0.2f);
        Entity hall = _builder.Parts.Find(inn, "shot.1");

        _builder.Teleport(hero, new Vector3(0, 0.3f, 0));
        _builder.Run(0.2f);
        Entity inside = _builder.Director.Live;
        _builder.Teleport(hero, new Vector3(0, 0.3f, -8));
        _builder.Run(0.2f);

        inside.Should().Be(hall);
        _builder.Director.Live.Should().Be(street);
    }

    [Fact]
    public void ADoubleDoorsSecondLeafOpensWithTheFirstAndUsingItUsesTheDoor()
    {
        BuildingFile building = BuildingFile.Create();
        BuildingFileTestsBuilder.FrontDoor(building).Double = true;
        BuildingMaker.Make(building, null);
        _builder.Write(building);
        Entity inn = _builder.Place();
        Entity first = _builder.Parts.Find(inn, "storey.0.door.9");
        Entity second = _builder.Parts.Find(inn, "storey.0.door.9.b");
        Quaternion shut = _builder.PlaceOf(second).Rotation;

        _builder.Game.Events.Send(new InteractionActionEvent(Entity.Null, second, Toggle));
        _builder.Run(1);

        _builder.World.Has<Toggle>(second).Should().BeFalse("only the first leaf is the door");
        _builder.World.Has<ActiveInteractable>(second).Should().BeTrue("the second leaf offers the door's action");
        _builder.World.Get<Toggle>(first).Open.Should().BeTrue();
        _builder.PlaceOf(second).Rotation.Should().NotBe(shut);
        float firstTurn = _builder.World.Get<JointPose>(first).Position;
        _builder.World.Get<JointPose>(second).Position.Should().Be(firstTurn);
        Vector3 reachFirst = InteractionSystem.GetUsePoint(_builder.World, first);
        Vector3 reachSecond = InteractionSystem.GetUsePoint(_builder.World, second);
        reachFirst.Should().NotBe(reachSecond, "each leaf is reached at its own handle");
    }

    [Fact]
    public void AWindowsGlassAndTheGlassInALeafAreDrawnSeeThroughAndTheLeafsGoesWithIt()
    {
        BuildingFile building = BuildingFile.Create();
        BuildingStorey ground = building.Storeys[0];
        PlanOpening front = BuildingFileTestsBuilder.FrontDoor(building);
        front.WindowWidth = 0.4f;
        ground.Openings.Add(new PlanOpening { Id = 30, Name = "Window", Kind = OpeningKind.Window, Wall = ground.Walls[2].Id, Along = 3, Lift = 0.9f, Height = 1.2f });
        BuildingMaker.Make(building, static (solid, _) => solid);
        _builder.Write(building);

        Entity inn = _builder.Place();

        Entity pane = _builder.Parts.Find(inn, "storey.0.glass.30");
        Entity leafPane = _builder.Parts.Find(inn, "storey.0.door.9.glass");
        _builder.World.Get<MeshRenderer>(pane).Materials.Should().Equal(BuildingFile.GlassMaterial);
        _builder.World.Get<MeshRenderer>(leafPane).Materials.Should().Equal(BuildingFile.GlassMaterial);
        _builder.World.Get<PartFollower>(leafPane).Leader.Should().Be("storey.0.door.9");
        _builder.World.Has<RigidBody>(leafPane).Should().BeFalse("the leaf is what blocks the way");
    }
}
