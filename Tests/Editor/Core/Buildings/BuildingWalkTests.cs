using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction;
using FlickerVoid.Engine.Interaction.Interactors;

namespace FlickerVoid.Editor.Core.Tests.Buildings;

/// <summary>Buildings the creator's tools made, cut with the Booleans, walked through in a playing game.</summary>
public sealed class BuildingWalkTests : IDisposable
{
    private static readonly Tag Toggle = new("Interaction.Action.Toggle");

    private readonly BuildingWalkTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ACharacterWalksInThroughTheFrontDoorOnceItIsOpen()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = BuildingFile.Create();
        BuildingTools.Rebuild(building);
        _builder.Place(building);
        float ground = building.GetFloorHeight(0);
        Entity hero = _builder.AddWalker(new Vector3(0, 0, -6));
        Entity door = _builder.Parts.Find(_builder.House, "storey.0.door.9");
        _builder.Run(0.2f);

        _builder.Game.Events.Send(new InteractionActionEvent(hero, door, Toggle));
        _builder.Run(1.5f);
        _builder.Game.Events.Send(new MoveCommand(hero, new Vector3(1.5f, ground, 1)));
        _builder.Run(10);

        Vector3 reached = _builder.PlaceOf(hero);
        reached.X.Should().BeApproximately(1.5f, 0.25f);
        reached.Z.Should().BeApproximately(1, 0.25f, "it came in through the open door in the front wall");
        reached.Y.Should().BeApproximately(ground, 0.1f);
    }

    [Fact]
    public void ACharacterWalksUpTheStairsTheToolsAddedToTheStoreyAbove()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = BuildingFile.Create();
        building.Size = new Vector3(10, 3, 8);
        building.Storeys.Clear();
        building.Storeys.Add(building.CreateStorey(0));
        BuildingTools.SetStoreys(building, 2, false);
        Vector2 foot = BuildingTools.GetStairsFoot(building, 0);
        BuildingTools.AddStairs(building, 0, foot, 0, StairsKind.Straight);
        BuildingTools.Rebuild(building);
        _builder.Place(building);
        float ground = building.GetFloorHeight(0);
        float upper = building.GetFloorHeight(1);
        Entity hero = _builder.AddWalker(new Vector3(2, ground, 0));
        _builder.Run(0.2f);

        _builder.Game.Events.Send(new MoveCommand(hero, new Vector3(2, upper, 0)));
        _builder.Run(14);

        Vector3 reached = _builder.PlaceOf(hero);
        reached.Y.Should().BeApproximately(upper, 0.1f, "it climbed to the upper floor");
        reached.X.Should().BeApproximately(2, 0.25f);
        reached.Z.Should().BeApproximately(0, 0.25f);
    }

    [Fact]
    public void ACharacterRidesTheElevatorUpAndWalksOffOnTheUpperFloor()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = BuildingFile.Create();
        BuildingTools.SetStoreys(building, 2, false);
        Vector2 shaft = BuildingTools.GetElevatorPlace(building);
        int id = BuildingTools.AddElevator(building, shaft, 270);
        BuildingTools.Rebuild(building);
        _builder.Place(building);
        float ground = building.GetFloorHeight(0);
        float upper = building.GetFloorHeight(1);
        Entity platform = _builder.Parts.Find(_builder.House, $"elevator.{id}");
        Entity hero = _builder.AddWalker(new Vector3(0, ground, 0));
        _builder.Run(0.2f);

        Vector3 stand = InteractionSystem.GetUsePoint(_builder.Game.World, platform);
        _builder.Game.Events.Send(new MoveCommand(hero, stand));
        _builder.Run(5);
        _builder.Game.Events.Send(new InteractionActionEvent(hero, platform, Toggle));
        _builder.Run(4);
        float ridden = _builder.PlaceOf(hero).Y;
        _builder.Game.Events.Send(new MoveCommand(hero, new Vector3(0, upper, 0)));
        _builder.Run(6);

        ridden.Should().BeApproximately(upper, 0.15f, "it rode the platform to the upper floor");
        Vector3 reached = _builder.PlaceOf(hero);
        reached.Y.Should().BeApproximately(upper, 0.1f);
        reached.X.Should().BeApproximately(0, 0.25f, "it walked off the platform onto the upper floor");
    }

    [Fact]
    public void PathsLeaveTheShaftOpenAndReachEveryFloorAroundIt()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = BuildingFile.Create();
        building.Size = new Vector3(10, 3, 8);
        building.Storeys.Clear();
        building.Storeys.Add(building.CreateStorey(0));
        BuildingTools.SetStoreys(building, 2, false);
        Vector2 foot = BuildingTools.GetStairsFoot(building, 0);
        BuildingTools.AddStairs(building, 0, foot, 0, StairsKind.Straight);
        Vector2 shaft = BuildingTools.GetElevatorPlace(building);
        BuildingTools.AddElevator(building, shaft, 270);
        BuildingTools.Rebuild(building);
        _builder.Place(building);
        float ground = building.GetFloorHeight(0);
        float upper = building.GetFloorHeight(1);
        List<Vector3> corners = [];

        bool found = _builder.Paths.TryFindPath(new Vector3(0, ground, 0), new Vector3(0, upper, 2), corners);

        found.Should().BeTrue();
        corners[^1].Y.Should().BeApproximately(upper, 0.2f);
    }
}
