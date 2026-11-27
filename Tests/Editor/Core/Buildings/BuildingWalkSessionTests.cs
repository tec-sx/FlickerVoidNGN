using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Interaction.Toggles;

namespace FlickerVoid.Editor.Core.Tests.Buildings;

public sealed class BuildingWalkSessionTests : IDisposable
{
    private readonly BuildingWalkSessionTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheWalkerStartsOutsideTheFrontDoor()
    {
        BuildingFile building = BuildingFile.Create();

        Vector3 start = BuildingWalk.GetStart(building);

        start.X.Should().BeApproximately(0, 1e-3f);
        start.Z.Should().BeApproximately(-4.85f, 1e-3f, "two metres out from the front wall's middle");
    }

    [Fact]
    public void AClickOnTheDoorWalksToItAndOpensItAndOnceInsideClicksPassThroughTheRoof()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = BuildingFile.Create();
        BuildingTools.Rebuild(building);
        BuildingWalk walk = _builder.Build(building);
        Entity door = walk.Game.Services.GetService<CreationParts>()!.Find(walk.Building, "storey.0.door.9");

        walk.Click(BuildingWalkSessionTestsBuilder.Toward(new Vector3(0, 1.2f, -2.85f), new Vector3(0, 6, -12)));
        walk.Run(3);
        bool open = walk.World.Get<Toggle>(door).Open;
        walk.Click(BuildingWalkSessionTestsBuilder.Toward(new Vector3(0, 0.2f, -1.5f), new Vector3(0, 1.5f, -8)));
        walk.Run(4);
        walk.Click(BuildingWalkSessionTestsBuilder.Down(new Vector3(1.5f, 0.2f, 1)));
        walk.Run(6);

        open.Should().BeTrue("the walker reached the door and used it");
        Vector3 reached = _builder.PlaceOf(walk);
        reached.X.Should().BeApproximately(1.5f, 0.3f);
        reached.Z.Should().BeApproximately(1, 0.3f);
    }

    [Fact]
    public void TheHallsShotGoesLiveWhenTheWalkerEntersTheHall()
    {
        Assert.SkipUnless(SolidBoolean.IsAvailable, "The Booleans' native library is not here.");
        BuildingFile building = BuildingFile.Create();
        building.Storeys[0].Openings[0].Kind = OpeningKind.Doorway;
        int id = BuildingTools.AddShot(building, 0, new Vector3(0, 10, -10), Quaternion.Identity);
        building.Shots[0].Name = "Hall shot";
        building.Shots[0].Triggers.Add(new ShotTrigger { Target = TriggerTarget.Room, Level = 0, Room = "Hall" });
        BuildingTools.Rebuild(building);
        BuildingWalk walk = _builder.Build(building);
        string before = walk.LiveShot;

        walk.Click(BuildingWalkSessionTestsBuilder.Down(new Vector3(0, 0.2f, 0)));
        walk.Run(6);

        id.Should().Be(1);
        before.Should().Be("Walk Outside");
        walk.LiveShot.Should().NotBe("Walk Outside", "the hall's trigger switched to its shot");
        walk.View.Should().NotBeNull();
    }
}
