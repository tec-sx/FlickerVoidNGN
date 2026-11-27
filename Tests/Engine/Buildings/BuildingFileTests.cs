using System.Numerics;
using System.Text.Json;

using FlickerVoid.Engine.Buildings.Plans;

namespace FlickerVoid.Engine.Buildings.Tests;

public sealed class BuildingFileTests
{
    [Fact]
    public void ANewBuildingIsAGroundFloorOfOneRoomWithAFrontDoorUnderARoof()
    {
        BuildingFile building = BuildingFile.Create();

        BuildingMaker.Make(building, null);

        building.GetParts().Select(part => part.Id).Should().Equal("storey.0.floor", "storey.0.walls", "storey.0.door.9", "roof");
        BuildingStorey ground = building.Storeys.Single();
        ground.Walls.Should().HaveCount(4).And.OnlyContain(wall => wall.Outside);
        ground.Rooms.Single().Name.Should().Be("Hall");
        building.Roof.Part!.Place.Position.Y.Should().Be(3);
        building.HasCellar.Should().BeFalse();
        building.StoreysAbove.Should().Be(1);
    }

    [Fact]
    public void StoreysStandOnOneAnotherAndTheCellarUnderTheGround()
    {
        BuildingFile building = BuildingFileTestsBuilder.WithStoreys(3, cellar: true);

        building.GetBase(-1).Should().Be(-3);
        building.GetBase(2).Should().Be(6);
        building.GetFloorHeight(1).Should().BeApproximately(3.2f, 1e-5f);
        building.GetTop().Should().Be(9);
        building.Roof.Part!.Place.Position.Y.Should().Be(9);
        building.Storeys[0].Parts["floor"].Place.Position.Y.Should().Be(-3);
    }

    [Fact]
    public void StairsStandOnTheirStoreysFloorAndTheElevatorStopsAtEveryStoreyItSpans()
    {
        BuildingFile building = BuildingFileTestsBuilder.WithEverything();

        building.GetParts().Select(part => part.Id).Should().Contain(["stairs.1", "stairs.1.railing", "elevator.1", "elevator.1.railing", "elevator.1.call.-1", "elevator.1.call.0", "elevator.1.call.1"]);
        building.Stairs[0].Part!.Place.Position.Should().Be(new Vector3(-3, 0.2f, -2));
        building.Elevators[0].Platform!.Place.Position.Should().Be(new Vector3(2, -2.8f, 1));
        building.GetElevatorStops(building.Elevators[0]).Select(stop => stop.Height).Should().Equal(0, 3, 6);
    }

    [Fact]
    public void AFileReadsBackAsItWasWritten()
    {
        BuildingFile building = BuildingFileTestsBuilder.WithEverything();
        building.Shots.Add(BuildingFileTestsBuilder.HallShot(TriggerMoment.Both));

        string json = BuildingFileTestsBuilder.Write(building);
        BuildingFile read = BuildingFileTestsBuilder.Read(json);

        read.GetParts().Select(part => part.Id).Should().Equal(building.GetParts().Select(part => part.Id));
        read.Storeys[0].Level.Should().Be(-1);
        read.Elevators[0].Speed.Should().Be(1.5f);
        read.Stairs[0].Part!.Mesh.Points.Should().Equal(building.Stairs[0].Part!.Mesh.Points);
        read.Shots[0].Triggers[0].When.Should().Be(TriggerMoment.Both);
        read.Storeys[1].Openings[0].Name.Should().Be("Front door");
    }

    [Fact]
    public void AFileWhoseLevelsDoNotFollowOneAnotherCantBeRead()
    {
        BuildingFile building = BuildingFileTestsBuilder.WithStoreys(2, cellar: false);
        building.Storeys[1].Level = 3;
        string json = BuildingFileTestsBuilder.Write(building);

        Action act = () => BuildingFileTestsBuilder.Read(json);

        act.Should().Throw<JsonException>().WithMessage("*follow one another*");
    }

    [Fact]
    public void AFileWrittenBeforeRoofsHadShapesKeepsItsRoofsSolid()
    {
        const string Json = """
            { "size": [8, 3, 6], "storeys": [ { "level": 0, "shell": { "mesh": { "points": [[0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1]], "faces": [[0, 1, 2], [0, 2, 3], [0, 3, 1], [1, 3, 2]] } } } ],
              "roof": { "place": { "position": [0, 3, 0] }, "mesh": { "points": [[0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1]], "faces": [[0, 1, 2], [0, 2, 3], [0, 3, 1], [1, 3, 2]] } } }
            """;

        BuildingFile read = BuildingFileTestsBuilder.Read(Json);

        read.Roof.Part.Should().NotBeNull();
        read.Roof.Part!.Place.Position.Y.Should().Be(3);
        read.Roof.Part.Mesh.Points.Should().HaveCount(4);
    }
}
