using System.Numerics;

using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;

namespace FlickerVoid.Editor.Core.Tests.Buildings;

public sealed class BuildingToolsTests
{
    [Fact]
    public void AddsStoreysOnTopWithTheWallsOfTheTopStoreyAndACellarUnderTheGround()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();

        BuildingTools.AddStoreyAbove(building);
        BuildingTools.AddCellar(building);

        building.Storeys.Select(storey => storey.Level).Should().Equal(-1, 0, 1);
        building.Storeys[2].Walls.Should().HaveCount(4);
        building.Storeys[2].Openings.Should().BeEmpty("the front door stays on the ground floor");
        building.Storeys[0].Rooms.Single().Name.Should().Be("Cellar");
    }

    [Fact]
    public void DuplicatesAStoreyOnTopWithItsOpenings()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().WithStoreys(2).Build();
        BuildingStorey first = building.FindStorey(1)!;
        (PlanWall wall, float along) = BuildingToolsTestsBuilder.WallAt(first, new Vector2(0, -2.85f));
        PlanEdits.AddOpening(first, wall.Id, along, OpeningKind.Window);

        int level = BuildingTools.DuplicateStorey(building, 1);

        level.Should().Be(2);
        building.FindStorey(2)!.Openings.Single().Kind.Should().Be(OpeningKind.Window);
    }

    [Fact]
    public void TakingAStoreyAwayMovesTheOnesAboveDownAndTakesItsStairsAndShots()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().WithStoreys(3).Build();
        BuildingTools.AddStairs(building, 0, new Vector2(-2, -1), 0, StairsKind.Straight);
        BuildingTools.AddStairs(building, 1, new Vector2(-2, -1), 0, StairsKind.Straight);
        BuildingTools.AddShot(building, 1, Vector3.Zero, Quaternion.Identity);
        BuildingTools.AddElevator(building, new Vector2(2, 0), 0);

        BuildingTools.RemoveStorey(building, 1);

        building.Storeys.Select(storey => storey.Level).Should().Equal(0, 1);
        building.Stairs.Should().BeEmpty();
        building.Shots.Should().BeEmpty();
        building.Elevators.Single().To.Should().Be(1);
    }

    [Fact]
    public void RebuildingCutsTheOpeningsThroughTheWallsAndTheStairsThroughTheFloorAbove()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = new BuildingToolsTestsBuilder().WithStoreys(2).Build();
        BuildingFile uncut = new BuildingToolsTestsBuilder().WithStoreys(2).Build();
        BuildingTools.AddStairs(building, 0, new Vector2(-2, -1.5f), 0, StairsKind.Straight);
        BuildingMaker.Make(uncut, null);

        BuildingTools.Rebuild(building);

        BuildingStorey ground = building.FindStorey(0)!;
        ground.Parts["walls"].Mesh.Points.Count.Should().BeGreaterThan(uncut.FindStorey(0)!.Parts["walls"].Mesh.Points.Count, "the door is cut through the front wall");
        building.FindStorey(1)!.Parts["floor"].Mesh.Points.Count.Should().BeGreaterThan(uncut.FindStorey(1)!.Parts["floor"].Mesh.Points.Count, "the stairs come up through the floor");
        ground.Parts.Should().ContainKey("door.9");
        building.Stairs.Single().RailingPart.Should().NotBeNull();
    }

    [Fact]
    public void PillarsStandInTheRoomsAndAtTheOutsideCornersOnRequest()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);
        PlanEdits.AddPillar(ground, new Vector2(1, 1), 0.4f, round: true);
        ground.CornerPillars = true;

        BuildingMaker.Make(building, null);

        ground.Parts.Keys.Should().Contain(["pillar.10", "corner.1", "corner.2", "corner.3", "corner.4"]);
        ground.Parts["pillar.10"].Place.Position.Should().Be(new Vector3(1, 0.2f, 1));
        ground.Parts["corner.1"].Mesh.Points.Max(point => point.Y).Should().BeApproximately(2.8f, 1e-4f);
    }

    [Fact]
    public void AnElevatorStopsAtEveryStoreyAndHasACallButtonOnEach()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().WithStoreys(3).WithCellar().Build();

        BuildingTools.AddElevator(building, new Vector2(2, 0), 0);
        BuildingMaker.Make(building, null);

        BuildingElevator elevator = building.Elevators.Single();
        (elevator.From, elevator.To).Should().Be((-1, 2));
        building.GetParts().Select(part => part.Id).Count(id => id.StartsWith("elevator.1.call.", StringComparison.Ordinal)).Should().Be(4);
    }

    [Fact]
    public void AShotLooksAtThePointFromItsDirection()
    {
        (Vector3 position, Quaternion rotation) = BuildingTools.LookAt(new Vector3(0, 0, 0), 0, -0.5f, 10);

        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        (position + (forward * 10)).Length().Should().BeLessThan(1e-4f);
        position.Y.Should().BeGreaterThan(0, "it looks down");
    }
}
