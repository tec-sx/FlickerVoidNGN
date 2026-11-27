using System.Numerics;

using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings.Tests;

public sealed class PlanGeometryTests
{
    [Fact]
    public void TheFloorIsLaidUnderTheOutsideWallsOuterFaces()
    {
        BuildingFile building = BuildingFile.Create();
        BuildingStorey ground = building.Storeys[0];

        List<Vector2> outline = PlanGeometry.GetOutline(ground);
        List<Vector2> inside = PlanGeometry.GetInsideOutline(ground);

        Polygons.Area(outline).Should().BeApproximately(8 * 6, 1e-3f);
        Polygons.Area(inside).Should().BeApproximately(7.4f * 5.4f, 1e-3f);
    }

    [Fact]
    public void AnInsideWallDividesTheStoreyIntoTwoRoomsWithTheirAreaInsideTheWalls()
    {
        BuildingStorey ground = BuildingPlanTestsBuilder.WithDividingWall();

        List<RoomShape> rooms = PlanGeometry.FindRooms(ground);

        rooms.Should().HaveCount(2);
        rooms.Sum(room => room.Area).Should().BeApproximately((7.4f * 5.4f) - (5.4f * 0.3f), 0.5f);
        rooms.Should().ContainSingle(room => room.Record == 0, "the hall's point lies in one of them");
    }

    [Fact]
    public void AWallIsAClosedSolidMitredAtItsCorners()
    {
        BuildingStorey ground = BuildingFile.Create().Storeys[0];

        SolidMesh wall = PlanGeometry.CreateWall(ground, ground.Walls[0], 0, 2.8f)!;

        BuildingPlanTestsBuilder.IsClosed(wall).Should().BeTrue();
        wall.Points.Max(point => point.X).Should().BeApproximately(4, 1e-4f, "the mitre reaches the outer corner");
        wall.Points.Max(point => point.Y).Should().BeApproximately(2.8f, 1e-4f);
    }

    [Fact]
    public void AnArchedWallBendsItsMiddleToItsLeft()
    {
        List<Vector2> path = PlanGeometry.GetPath(new Vector2(0, 0), new Vector2(4, 0), 1, 8);

        path.Should().HaveCount(9);
        path[4].X.Should().BeApproximately(2, 1e-4f);
        path[4].Y.Should().BeApproximately(-1, 1e-4f, "left of walking toward +X, seen from above, is -Z");
    }

    [Fact]
    public void AnOpeningFitsOnlyClearOfItsWallsEndsAndUnderItsTop()
    {
        BuildingFile building = BuildingFile.Create();
        BuildingStorey ground = building.Storeys[0];
        PlanOpening door = BuildingFileTestsBuilder.FrontDoor(building);

        bool middle = OpeningShape.Fits(building, ground, door);
        door.Along = 0.3f;
        bool atTheEnd = OpeningShape.Fits(building, ground, door);
        door.Along = 3.85f;
        door.Height = 3;
        bool tooTall = OpeningShape.Fits(building, ground, door);

        middle.Should().BeTrue();
        atTheEnd.Should().BeFalse();
        tooTall.Should().BeFalse();
    }

    [Fact]
    public void AnArchedOpeningsOutlineRisesToItsArch()
    {
        PlanOpening window = new() { Width = 1, Height = 2, Top = OpeningTop.Arch, ArchRise = 0.5f };

        List<Vector2> profile = OpeningShape.GetProfile(window, 0);

        profile.Max(point => point.Y).Should().BeApproximately(2, 1e-4f);
        profile.Should().Contain(point => MathF.Abs(point.X - 0.5f) < 1e-4f && MathF.Abs(point.Y - 1.5f) < 1e-4f, "the side ends where the arch starts");
    }

    [Fact]
    public void ADoorsLeafSwingsToTheSideItOpensTo()
    {
        BuildingFile building = BuildingFile.Create();
        BuildingStorey ground = building.Storeys[0];
        PlanOpening door = BuildingFileTestsBuilder.FrontDoor(building);
        OpeningShape.TryGetFrame(building, ground, door, out OpeningFrame frame).Should().BeTrue();

        DoorLeaf leaf = OpeningShape.MakeLeaf(frame, door);
        Matrix4x4 open = Matrix4x4.CreateFromAxisAngle(leaf.Axis, float.DegreesToRadians(90));
        Vector3 swung = Vector3.Transform(leaf.Middle, open) + leaf.Pivot;

        frame.Out.Z.Should().BeApproximately(-1, 1e-4f, "the front wall's outside is toward -Z");
        swung.Z.Should().BeLessThan(leaf.Pivot.Z - 0.3f, "an outward door swings out of the front");
        BuildingPlanTestsBuilder.IsClosed(leaf.Mesh).Should().BeTrue();
    }

    [Fact]
    public void StairsClimbTheirHeightInEvenStepsAndOpenTheFloorAboveOverThem()
    {
        BuildingStairs stairs = new() { Rise = 0.15f, Tread = 0.28f };

        StairsLayout layout = StairsLayout.Create(stairs, 3, 0.2f);

        layout.Steps.Should().Be(20);
        layout.StepRise.Should().BeApproximately(0.15f, 1e-5f);
        layout.Blocks.Should().HaveCount(19, "the top step is the floor above");
        layout.Openings.Should().NotBeEmpty();
        BuildingPlanTestsBuilder.IsClosed(layout.CreateSolid()).Should().BeTrue();
    }

    [Theory]
    [InlineData(StairsKind.Straight)]
    [InlineData(StairsKind.L)]
    [InlineData(StairsKind.U)]
    public void TheOpeningInTheFloorAboveEndsWhereTheStairsArrive(StairsKind kind)
    {
        BuildingStairs stairs = new() { Kind = kind, Margin = 0.05f };

        StairsLayout layout = StairsLayout.Create(stairs, 3, 0.2f);

        (Vector2 Low, Vector2 High, float Top) top = layout.Blocks.MaxBy(block => block.Top);
        (Vector2 Low, Vector2 High) arrival = layout.Openings[^1];
        bool flush = MathF.Abs(arrival.High.Y - top.High.Y) < 1e-4f || MathF.Abs(arrival.Low.Y - top.Low.Y) < 1e-4f
            || MathF.Abs(arrival.High.X - top.High.X) < 1e-4f || MathF.Abs(arrival.Low.X - top.Low.X) < 1e-4f;
        flush.Should().BeTrue("the floor above meets the top step with no gap to fall through");
    }

    [Theory]
    [InlineData(StairsKind.L)]
    [InlineData(StairsKind.U)]
    public void TurningStairsHaveALandingAndClimbTheSameHeight(StairsKind kind)
    {
        BuildingStairs stairs = new() { Kind = kind };

        StairsLayout layout = StairsLayout.Create(stairs, 3, 0.2f);

        layout.Blocks.Max(block => block.Top).Should().BeApproximately(3 - layout.StepRise, 1e-4f);
        layout.Blocks.Should().Contain(block => block.High.X - block.Low.X > stairs.Width - 0.01f && block.High.Y - block.Low.Y > stairs.Width - 0.01f, "a landing as wide as the stairs both ways");
    }

    [Fact]
    public void ConcavePrismsAreClosedSolids()
    {
        List<Vector2> shape = [new(0, 0), new(4, 0), new(4, 4), new(2, 2), new(0, 4)];

        SolidMesh prism = Polygons.Prism(shape, 0, 1);

        BuildingPlanTestsBuilder.IsClosed(prism).Should().BeTrue();
        prism.Faces.Should().OnlyContain(face => face.Count <= 4);
    }
}
