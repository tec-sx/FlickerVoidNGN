using System.Numerics;

using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Engine.Buildings;

namespace FlickerVoid.Editor.Core.Tests.Buildings;

public sealed class BuildingToolsTests
{
    private readonly BuildingToolsTestsBuilder _builder = new();

    [Fact]
    public void AddsStairsAndOpensTheFloorAboveThem()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = _builder.WithStoreys(2).Build();
        int faces = building.Storeys[1].Shell.Mesh.Faces.Count;
        Vector2 foot = BuildingTools.GetStairsFoot(building, 0);

        string? id = BuildingTools.AddStairs(building, 0, foot, BuildingTools.StairsRun);

        id.Should().Be("stairs.1");
        building.Storeys[1].Shell.Mesh.Faces.Count.Should().BeGreaterThan(faces, "the floor above has an opening");
        building.Storeys[0].Shell.Mesh.Faces.Count.Should().Be(faces, "the floor the stairs stand on is whole");
    }

    [Fact]
    public void AddsNoStairsFromTheTopStorey()
    {
        BuildingFile building = _builder.WithStoreys(2).Build();

        string? id = BuildingTools.AddStairs(building, 1, Vector2.Zero, BuildingTools.StairsRun);

        id.Should().BeNull();
        building.Stairs.Should().BeEmpty();
    }

    [Fact]
    public void StartsFlightsOfNeighbouringLevelsInDifferentLanes()
    {
        BuildingFile building = _builder.WithStoreys(3).Build();

        Vector2 ground = BuildingTools.GetStairsFoot(building, 0);
        Vector2 first = BuildingTools.GetStairsFoot(building, 1);
        Vector2 second = BuildingTools.GetStairsFoot(building, 2);

        MathF.Abs(first.X - ground.X).Should().BeGreaterThan(1.2f, "a flight's foot stands clear of the opening below it");
        second.Should().Be(ground);
    }

    [Fact]
    public void AddsAnElevatorAndCutsItsShaftThroughEveryFloorAboveTheLowest()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = _builder.WithStoreys(3).Build();
        int faces = building.Storeys[0].Shell.Mesh.Faces.Count;
        Vector2 place = BuildingTools.GetElevatorPlace(building);

        BuildingTools.AddElevator(building, place);

        building.Elevator.Should().NotBeNull();
        building.Storeys[0].Shell.Mesh.Faces.Count.Should().Be(faces);
        building.Storeys[1].Shell.Mesh.Faces.Count.Should().BeGreaterThan(faces);
        building.Storeys[2].Shell.Mesh.Faces.Count.Should().BeGreaterThan(faces);
    }

    [Fact]
    public void KeepsTheElevatorOnTheLowestFloorWhenACellarIsAdded()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = _builder.WithStoreys(2).Build();
        int faces = building.Storeys[0].Shell.Mesh.Faces.Count;
        Vector2 place = BuildingTools.GetElevatorPlace(building);
        BuildingTools.AddElevator(building, place);

        BuildingTools.SetStoreys(building, 2, true);

        float rest = building.GetFloorHeight(-1);
        building.Elevator!.Platform.Place.Position.Y.Should().BeApproximately(rest, 1e-4f);
        building.Storeys[0].Shell.Mesh.Faces.Count.Should().Be(faces, "the cellar's floor is whole");
        building.Storeys[1].Shell.Mesh.Faces.Count.Should().BeGreaterThan(faces, "the ground floor is cut for the shaft");
    }

    [Fact]
    public void TakesAwayStairsThatLostTheirFloor()
    {
        BuildingFile building = _builder.WithStoreys(3).Build();
        BuildingTools.AddStairs(building, 0, BuildingTools.GetStairsFoot(building, 0), BuildingTools.StairsRun);
        BuildingTools.AddStairs(building, 1, BuildingTools.GetStairsFoot(building, 1), BuildingTools.StairsRun);

        BuildingTools.SetStoreys(building, 2, false);

        building.Stairs.Should().ContainSingle("only the flight from the ground floor still has a floor above");
    }
}
