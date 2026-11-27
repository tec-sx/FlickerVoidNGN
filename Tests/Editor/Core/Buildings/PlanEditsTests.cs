using System.Numerics;

using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;

namespace FlickerVoid.Editor.Core.Tests.Buildings;

public sealed class PlanEditsTests
{
    [Fact]
    public void PushingAWallOutMovesItsCornersAndStretchesTheWallsMeetingIt()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);
        (PlanWall front, _) = BuildingToolsTestsBuilder.WallAt(ground, new Vector2(0, -2.85f));

        PlanEdits.PushWall(ground, front.Id, 1.2f);

        PlanGeometry.FindCorner(ground, front.From)!.Z.Should().BeApproximately(-4.05f, 1e-4f);
        PlanGeometry.FindCorner(ground, front.To)!.Z.Should().BeApproximately(-4.05f, 1e-4f);
        Polygons.Area(PlanGeometry.GetOutline(ground)).Should().BeApproximately(8 * 7.2f, 1e-3f);
        building.Storeys[0].Openings.Single().Wall.Should().Be(front.Id, "the door goes with its wall");
    }

    [Theory]
    [InlineData(true, 3.2f)]
    [InlineData(false, 2f)]
    public void PushingAWallCarriesTheOpeningsOnTheWallsItStretchesOrLeavesThemInPlace(bool carry, float along)
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);
        (PlanWall front, _) = BuildingToolsTestsBuilder.WallAt(ground, new Vector2(0, -2.85f));
        (PlanWall right, _) = BuildingToolsTestsBuilder.WallAt(ground, new Vector2(3.85f, 0));
        int id = PlanEdits.AddOpening(ground, right.Id, 2, OpeningKind.Window);

        PlanEdits.PushWall(ground, front.Id, 1.2f, carry);

        PlanOpening window = ground.Openings.Single(opening => opening.Id == id);
        window.Wall.Should().Be(right.Id);
        window.Along.Should().BeApproximately(along, 1e-3f);
    }

    [Fact]
    public void SplittingAWallAddsACornerAndKeepsEachOpeningOnItsHalf()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);
        (PlanWall front, _) = BuildingToolsTestsBuilder.WallAt(ground, new Vector2(0, -2.85f));
        PlanOpening door = ground.Openings.Single();

        int? corner = PlanEdits.SplitWall(ground, front.Id, 1.5f);

        corner.Should().NotBeNull();
        ground.Walls.Should().HaveCount(5);
        ground.Walls.Should().OnlyContain(wall => wall.Outside);
        door.Wall.Should().NotBe(front.Id, "the door stands past the split");
        door.Along.Should().BeApproximately(3.85f - 1.5f, 1e-4f);
    }

    [Fact]
    public void SplittingAnArcKeepsBothHalvesOnItsCircle()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);
        (PlanWall front, _) = BuildingToolsTestsBuilder.WallAt(ground, new Vector2(0, -2.85f));
        PlanEdits.SetBulge(ground, front.Id, -1);
        List<Vector2> before = PlanGeometry.GetPath(ground, front);
        float length = PlanGeometry.GetLength(before);

        PlanEdits.SplitWall(ground, front.Id, length / 2);

        PlanWall second = ground.Walls[^1];
        float split = PlanGeometry.GetLength(PlanGeometry.GetPath(ground, front)) + PlanGeometry.GetLength(PlanGeometry.GetPath(ground, second));
        split.Should().BeApproximately(length, 0.05f);
        MathF.Sign(second.Bulge).Should().Be(-1);
    }

    [Fact]
    public void DrawingAWallAcrossTheStoreyJoinsItsWallsAndMakesTwoRooms()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);

        List<int> drawn = PlanEdits.AddWall(ground, new Vector2(1, -2.85f), new Vector2(1, 2.85f), outside: false);

        drawn.Should().HaveCount(1);
        ground.Walls.Should().HaveCount(7, "the front and the back are split where it meets them");
        ground.Rooms.Should().HaveCount(2);
        ground.Rooms.Should().Contain(room => room.Name == "Hall");
        PlanGeometry.GetOutline(ground).Count.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void AWallCrossingAnotherSplitsBothWhereTheyCross()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);
        PlanEdits.AddWall(ground, new Vector2(1, -2.85f), new Vector2(1, 2.85f), outside: false);

        List<int> drawn = PlanEdits.AddWall(ground, new Vector2(-3.85f, 0), new Vector2(3.85f, 0), outside: false);

        drawn.Should().HaveCount(2, "it is split where it crosses the first wall");
        ground.Rooms.Should().HaveCount(4);
    }

    [Fact]
    public void DrawingARoomClosesItWithInsideWallsAndNamesIt()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);

        PlanEdits.AddRoom(ground, new Vector2(1, 0), new Vector2(3.85f, 2.85f), "Store");

        ground.Rooms.Should().Contain(room => room.Name == "Store");
        ground.Rooms.Should().Contain(room => room.Name == "Hall");
        PlanGeometry.FindRooms(ground).Should().HaveCount(2);
    }

    [Fact]
    public void TakingAWallOutJoinsItsRoomsAgain()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);
        int wall = PlanEdits.AddWall(ground, new Vector2(1, -2.85f), new Vector2(1, 2.85f), outside: false).Single();

        PlanEdits.RemoveWall(ground, wall);

        ground.Rooms.Should().HaveCount(1);
    }

    [Fact]
    public void TakingACornerBetweenTwoWallsOutMakesThemOne()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);
        (PlanWall front, _) = BuildingToolsTestsBuilder.WallAt(ground, new Vector2(0, -2.85f));
        int corner = PlanEdits.SplitWall(ground, front.Id, 1)!.Value;

        PlanEdits.RemoveCorner(ground, corner);

        ground.Walls.Should().HaveCount(4);
        ground.Corners.Should().HaveCount(4);
    }

    [Fact]
    public void OpeningsAreSizedForTheirKindAndHeldOnTheirWall()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);
        (PlanWall back, _) = BuildingToolsTestsBuilder.WallAt(ground, new Vector2(0, 2.85f));

        int window = PlanEdits.AddOpening(ground, back.Id, 0.1f, OpeningKind.Window);

        PlanOpening added = ground.Openings.Single(opening => opening.Id == window);
        added.Lift.Should().Be(0.9f);
        added.Name.Should().Be("Window 1");
        OpeningShape.Fits(building, ground, added).Should().BeTrue("it is moved clear of the wall's end");
    }
}
