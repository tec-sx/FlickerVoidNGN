using System.Numerics;

using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Core.Tests.Buildings;

public sealed class PlanPickerTests
{
    [Fact]
    public void ARayAtAWallsFacePicksThatWallHoweverHighItLands()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);
        (PlanWall back, _) = BuildingToolsTestsBuilder.WallAt(ground, new Vector2(0, 2.85f));
        float floor = building.GetFloorHeight(0);

        // From over the front wall, down onto the back wall's inside face near its top: the floor is met far behind the wall.
        Ray ray = Toward(new Vector3(0, floor + 8, -6), new Vector3(0, floor + 2.4f, 2.7f));
        PlanHit? hit = PlanPicker.Pick(building, 0, ray);

        hit.Should().NotBeNull();
        hit!.Value.Pick.Should().Be(new PlanPick(PlanPickKind.Wall, 0, back.Id));
        hit.Value.Point.Z.Should().BeApproximately(2.7f, 1e-3f);
        hit.Value.Point.Y.Should().BeApproximately(floor + 2.4f, 1e-3f);
    }

    [Fact]
    public void ARayAtADoorPicksTheOpeningAndItsWallWhenOnlyWallsAreTaken()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);
        PlanOpening door = ground.Openings.Single();
        float floor = building.GetFloorHeight(0);
        Ray ray = Toward(new Vector3(0, floor + 1.8f, -9), new Vector3(0, floor + 1.8f, 0));

        PlanHit? any = PlanPicker.Pick(building, 0, ray);
        PlanHit? wall = PlanPicker.Pick(building, 0, ray, static kind => kind == PlanPickKind.Wall);

        any!.Value.Pick.Should().Be(new PlanPick(PlanPickKind.Opening, 0, door.Id));
        wall!.Value.Pick.Should().Be(new PlanPick(PlanPickKind.Wall, 0, door.Wall));
    }

    [Fact]
    public void ARayAtTheStepsOfStairsPicksThemAndBesideThemTheRoom()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().WithStoreys(2).Build();
        int id = BuildingTools.AddStairs(building, 0, new Vector2(-1, -2), 0, StairsKind.Straight)!.Value;
        float floor = building.GetFloorHeight(0);

        // Along the flight from its side, onto a step well above the floor.
        Ray onSteps = Toward(new Vector3(3, floor + 1.6f, 0.6f), new Vector3(-1, floor + 1.2f, 0.6f));
        Ray beside = Toward(new Vector3(2, floor + 6, 0), new Vector3(2, floor, 0));

        PlanPicker.Pick(building, 0, onSteps)!.Value.Pick.Should().Be(new PlanPick(PlanPickKind.Stairs, 0, id));
        PlanPicker.Pick(building, 0, beside)!.Value.Pick.Should().Be(new PlanPick(PlanPickKind.Room, 0, 0, "Hall"));
    }

    [Fact]
    public void ATurnedElevatorIsPickedInsideItsTurnedSquare()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().WithStoreys(2).Build();
        int id = BuildingTools.AddElevator(building, new Vector2(0, 0), 45);
        float floor = building.GetFloorHeight(0);
        Ray onItsCorner = Toward(new Vector3(1, floor + 6, 0), new Vector3(1, floor, 0));
        Ray pastItsSide = Toward(new Vector3(0.75f, floor + 6, 0.75f), new Vector3(0.75f, floor, 0.75f));

        PlanPicker.Pick(building, 0, onItsCorner)!.Value.Pick.Should().Be(new PlanPick(PlanPickKind.Elevator, 0, id));
        PlanPicker.Pick(building, 0, pastItsSide)!.Value.Pick.Kind.Should().Be(PlanPickKind.Room);
    }

    [Fact]
    public void ARayAtAPillarPicksItAndPastEverythingNothing()
    {
        BuildingFile building = new BuildingToolsTestsBuilder().Build();
        BuildingStorey ground = BuildingToolsTestsBuilder.Ground(building);
        int id = PlanEdits.AddPillar(ground, new Vector2(1, 1), 0.4f, false);
        float floor = building.GetFloorHeight(0);
        Ray atPillar = Toward(new Vector3(-2, floor + 4, -2), new Vector3(1, floor + 1.5f, 0.9f));
        Ray outside = Toward(new Vector3(20, floor + 6, 20), new Vector3(20, floor, 20));

        PlanPicker.Pick(building, 0, atPillar)!.Value.Pick.Should().Be(new PlanPick(PlanPickKind.Pillar, 0, id));
        PlanPicker.Pick(building, 0, outside).Should().BeNull();
    }

    private static Ray Toward(Vector3 from, Vector3 to)
    {
        Vector3 direction = Vector3.Normalize(to - from);
        return new Ray(from, direction);
    }
}
