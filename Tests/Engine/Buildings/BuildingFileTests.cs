using System.Numerics;
using System.Text.Json;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings.Tests;

public sealed class BuildingFileTests
{
    [Fact]
    public void ANewBuildingIsAGroundFloorUnderARoof()
    {
        BuildingFile building = BuildingFile.Create();

        building.GetParts().Select(part => part.Id).Should().Equal("storey.0", "roof");
        building.Roof.Place.Position.Y.Should().Be(3);
        building.HasCellar.Should().BeFalse();
        building.StoreysAbove.Should().Be(1);
    }

    [Fact]
    public void AStoreysShellIsATrayOfFloorAndWallsOpenAtTheTop()
    {
        SolidMesh tray = BuildingShapes.CreateTray(new Vector3(8, 3, 6), 0.2f, 0.2f);

        tray.Points.Should().HaveCount(16);
        tray.Points.Min(point => point.X).Should().Be(-4);
        tray.Points.Max(point => point.Y).Should().Be(3);
        SolidTriangles triangles = tray.Triangulate();
        triangles.Vertices.Should().Contain(vertex => vertex.Position.Y == 0.2f && vertex.Normal.Y > 0.9f, "the floor inside looks up");
        triangles.Vertices.Should().Contain(vertex => vertex.Position.X == 3.8f && vertex.Normal.X < -0.9f, "the walls inside look in");
    }

    [Fact]
    public void StairsClimbTheirHeightInStepsCharactersCanWalk()
    {
        SolidMesh stairs = BuildingShapes.CreateStairs(1.2f, 3, 4);

        stairs.Points.Max(point => point.Y).Should().BeApproximately(3, 1e-4f);
        stairs.Points.Max(point => point.Z).Should().BeApproximately(4, 1e-4f);
        float[] heights = [.. stairs.Points.Select(point => MathF.Round(point.Y, 3)).Distinct().Order()];
        heights.Should().HaveCount(16, "fifteen steps of 0.2 and the ground");
        SolidTriangles triangles = stairs.Triangulate();
        triangles.Vertices.Count(vertex => vertex.Normal.Y > 0.9f).Should().Be(60, "each of the fifteen treads looks up");
    }

    [Fact]
    public void SetStoreysAddsStoreysOnTopAndACellarAndMovesTheRoof()
    {
        BuildingFile building = BuildingFile.Create();
        SolidMesh ground = building.Storeys[0].Shell.Mesh;

        building.SetStoreys(3, cellar: true);

        building.GetParts().Select(part => part.Id).Should().Equal("cellar", "storey.0", "storey.1", "storey.2", "roof");
        building.Storeys[0].Shell.Place.Position.Y.Should().Be(-3);
        building.Storeys[3].Shell.Place.Position.Y.Should().Be(6);
        building.Roof.Place.Position.Y.Should().Be(9);
        building.Storeys[1].Shell.Mesh.Should().BeSameAs(ground, "the storeys that stay keep their shape");
    }

    [Fact]
    public void SetStoreysTakesStoreysOffTheTopAndTheCellarAway()
    {
        BuildingFile building = BuildingFile.Create();
        building.SetStoreys(3, cellar: true);

        building.SetStoreys(1, cellar: false);

        building.GetParts().Select(part => part.Id).Should().Equal("storey.0", "roof");
        building.Roof.Place.Position.Y.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void RefusesAStoreyCountItDoesNotTake(int above)
    {
        BuildingFile building = BuildingFile.Create();

        Action act = () => building.SetStoreys(above, cellar: false);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void StairsStandOnTheirStoreysFloorAndTheElevatorOnTheLowest()
    {
        BuildingFile building = BuildingFileTestsBuilder.WithEverything();

        building.GetParts().Select(part => part.Id).Should().Equal("cellar", "storey.0", "storey.1", "roof", "stairs.1", "elevator");
        building.Stairs[0].Place.Position.Should().Be(new Vector3(-3, 0.2f, -2));
        building.Elevator!.Platform.Place.Position.Should().Be(new Vector3(2, -2.8f, 1));
        building.GetElevatorStops().Should().Equal(0, 3, 6);
    }

    [Fact]
    public void AFileReadsBackAsItWasWritten()
    {
        BuildingFile building = BuildingFileTestsBuilder.WithEverything();

        string json = BuildingFileTestsBuilder.Write(building);
        BuildingFile read = BuildingFileTestsBuilder.Read(json);

        read.GetParts().Select(part => part.Id).Should().Equal(building.GetParts().Select(part => part.Id));
        read.Storeys[0].Level.Should().Be(-1);
        read.Elevator!.Speed.Should().Be(1.5f);
        read.Stairs[0].Mesh.Points.Should().Equal(building.Stairs[0].Mesh.Points);
    }

    [Fact]
    public void AFileWhoseLevelsDoNotFollowOneAnotherCantBeRead()
    {
        BuildingFile building = BuildingFile.Create();
        building.SetStoreys(2, cellar: false);
        building.Storeys[1].Level = 3;
        string json = BuildingFileTestsBuilder.Write(building);

        Action act = () => BuildingFileTestsBuilder.Read(json);

        act.Should().Throw<JsonException>().WithMessage("*follow one another*");
    }
}
