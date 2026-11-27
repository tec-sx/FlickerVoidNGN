using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Characters.Movement;

namespace FlickerVoid.Editor.Core.Tests.Buildings;

public sealed class BuildingWalkTests : IDisposable
{
    private readonly BuildingWalkTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ACharacterWalksUpTheStairsTheToolsAddedToTheStoreyAbove()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = BuildingFile.Create();
        BuildingTools.SetStoreys(building, 2, false);
        Vector2 foot = BuildingTools.GetStairsFoot(building, 0);
        BuildingTools.AddStairs(building, 0, foot, BuildingTools.StairsRun);
        _builder.Place(building);
        float ground = building.GetFloorHeight(0);
        float upper = building.GetFloorHeight(1);
        Entity hero = _builder.AddWalker(new Vector3(2, ground, 0));
        _builder.Run(0.2f);

        _builder.Game.Events.Send(new MoveCommand(hero, new Vector3(2, upper, 0)));
        _builder.Run(12);

        Vector3 reached = _builder.PlaceOf(hero);
        reached.Y.Should().BeApproximately(upper, 0.1f, "it climbed to the upper floor");
        reached.X.Should().BeApproximately(2, 0.2f);
        reached.Z.Should().BeApproximately(0, 0.2f);
    }

    [Fact]
    public void ACharacterWalksInThroughTheEntrance()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = BuildingFile.Create();
        BuildingTools.AddEntrance(building);
        _builder.Place(building);
        float ground = building.GetFloorHeight(0);
        Entity hero = _builder.AddWalker(new Vector3(0, 0, -6));
        _builder.Run(0.2f);

        _builder.Game.Events.Send(new MoveCommand(hero, new Vector3(1, ground, 1)));
        _builder.Run(8);

        Vector3 reached = _builder.PlaceOf(hero);
        reached.X.Should().BeApproximately(1, 0.2f);
        reached.Z.Should().BeApproximately(1, 0.2f, "it came in through the doorway in the front wall");
        reached.Y.Should().BeApproximately(ground, 0.1f);
    }

    [Fact]
    public void PathsLeaveTheShaftOpenAndReachEveryFloorAroundIt()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = BuildingFile.Create();
        BuildingTools.SetStoreys(building, 2, false);
        Vector2 foot = BuildingTools.GetStairsFoot(building, 0);
        BuildingTools.AddStairs(building, 0, foot, BuildingTools.StairsRun);
        Vector2 shaft = BuildingTools.GetElevatorPlace(building);
        BuildingTools.AddElevator(building, shaft);
        _builder.Place(building);
        float ground = building.GetFloorHeight(0);
        float upper = building.GetFloorHeight(1);
        List<Vector3> corners = [];

        bool found = _builder.Paths.TryFindPath(new Vector3(0, ground, 0), new Vector3(0, upper, 2), corners);

        found.Should().BeTrue();
        corners[^1].Y.Should().BeApproximately(upper, 0.2f);
    }
}
