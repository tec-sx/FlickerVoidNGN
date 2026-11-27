using System.Numerics;

using FlickerVoid.Editor.Assets.Buildings;
using FlickerVoid.Editor.Assets.Buildings.Rows;
using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;

namespace FlickerVoid.Editor.Assets.Tests.Buildings;

public sealed class BuildingToolViewModelTests : IDisposable
{
    private const int FrontWall = 5;
    private const int FrontDoor = 9;

    private readonly BuildingToolViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsThePushWallToolOnThePickedWallAsTheMockupHasIt()
    {
        BuildingToolViewModel tool = _builder.Build();

        _builder.Editor.Tool = BuildingTool.PushWall;
        _builder.Editor.Pick(new PlanPick(PlanPickKind.Wall, 0, FrontWall));

        tool.Title.Should().Be("Push wall");
        tool.Subtitle.Should().Be("Outside wall 5, Ground");
        tool.Sections.Select(static section => section.Title).Should().Equal("Push wall", "This wall", "Follows");
        tool.Sections[0].Rows.Select(static row => row.Label).Should().Equal("Distance", "Snap to grid", "Carry openings");
        tool.Sections[1].Rows.Select(static row => row.Label).Should().StartWith(["Length", "Thickness", "Height"]);
        tool.Sections[2].Rows.Select(static row => row.Label).Should().Contain(["Outside wall 6", "Outside wall 8", "Floor", "Hall"]);
    }

    [Fact]
    public void ADistanceTypedInWaitsForApplyAndCancelPutsTheWallBack()
    {
        BuildingToolViewModel tool = _builder.Build();
        _builder.Editor.Tool = BuildingTool.PushWall;
        _builder.Editor.Pick(new PlanPick(PlanPickKind.Wall, 0, FrontWall));
        NumberRow distance = (NumberRow)tool.Sections[0].Rows[0];

        distance.Value = 1.2;
        bool pending = tool.IsPending;
        float pushed = _builder.Load().Storeys[0].Corners[0].Z;
        tool.CancelCommand.Execute(null);

        pending.Should().BeTrue();
        tool.PendingName.Should().BeEmpty();
        pushed.Should().BeApproximately(-4.05f, 1e-4f);
        _builder.Load().Storeys[0].Corners[0].Z.Should().BeApproximately(-2.85f, 1e-4f);
    }

    [Fact]
    public void WhileAPushWaitsTheFollowsShowWhatTheyWereAndWhatTheyBecome()
    {
        BuildingToolViewModel tool = _builder.Build();
        _builder.Editor.Tool = BuildingTool.PushWall;
        _builder.Editor.Pick(new PlanPick(PlanPickKind.Wall, 0, FrontWall));
        NumberRow distance = (NumberRow)tool.Sections[0].Rows[0];

        distance.Value = 1.2;

        ((NumberRow)tool.Sections[0].Rows[0]).Value.Should().BeApproximately(1.2, 1e-4);
        ListRow side = (ListRow)tool.Sections[2].Rows.First(static row => row.Label == "Outside wall 6");
        side.Value.Should().MatchRegex(@"^\d+\.\d\d → \d+\.\d\d m$");
        ListRow hall = (ListRow)tool.Sections[2].Rows.First(static row => row.Label == "Hall");
        hall.Value.Should().Contain("→");
    }

    [Fact]
    public void PickingStairsOrAShotOnAnotherStoreyGoesToThatStorey()
    {
        _builder.Build();
        int level = BuildingTools.AddStoreyAbove(_builder.Document.Building);
        int shot = BuildingTools.AddShot(_builder.Document.Building, level, new Vector3(0, 5, 0), Quaternion.Identity);

        _builder.Editor.Pick(new PlanPick(PlanPickKind.Shot, level, shot));

        _builder.Editor.Level.Should().Be(level);
    }

    [Fact]
    public void AppliedPushIsOneUndoStep()
    {
        BuildingToolViewModel tool = _builder.Build();
        _builder.Editor.Tool = BuildingTool.PushWall;
        _builder.Editor.Pick(new PlanPick(PlanPickKind.Wall, 0, FrontWall));
        NumberRow distance = (NumberRow)tool.Sections[0].Rows[0];

        distance.Value = 0.5;
        distance = (NumberRow)tool.Sections[0].Rows[0];
        distance.Value = 1;
        tool.ApplyCommand.Execute(null);

        _builder.Load().Storeys[0].Corners[0].Z.Should().BeApproximately(-3.85f, 1e-4f);
        _builder.Document.Building.Storeys[0].Corners[0].Z.Should().BeApproximately(-3.85f, 1e-4f);
    }

    [Fact]
    public void LocksTheDoorWithItsKeyFromItsLeafSection()
    {
        BuildingToolViewModel tool = _builder.Build();
        _builder.Editor.Tool = BuildingTool.Opening;
        _builder.Editor.Pick(new PlanPick(PlanPickKind.Opening, 0, FrontDoor));
        ToolSection leaf = tool.Sections.Single(static section => section.Title == "Leaf");

        ((ToggleRow)leaf.Rows.Single(static row => row.Label == "Locked")).IsOn = true;
        leaf = tool.Sections.Single(static section => section.Title == "Leaf");
        ((TextRow)leaf.Rows.Single(static row => row.Label == "Key tag")).Text = "Key.Inn";

        PlanOpening door = _builder.Load().Storeys[0].Openings.Single();
        door.Locked.Should().BeTrue();
        door.Key.Name.Should().Be("Key.Inn");
        tool.IsPending.Should().BeFalse("a change in the tab is its own undo step when nothing waits");
        tool.Sections.Select(static section => section.Title).Should().Equal("Opening", "Place", "Size", "Leaf", "Frame and window", "Cut");
    }

    [Fact]
    public void ACameraDroppedOnTheHallLooksAtItAndSwitchesWhenTheHallIsEntered()
    {
        BuildingToolViewModel tool = _builder.Build();
        _builder.Editor.Tool = BuildingTool.Camera;

        _builder.Editor.DropShot(new Vector2(0, 0));

        BuildingShot shot = _builder.Load().Shots.Single();
        shot.Name.Should().Be("Hall shot");
        shot.Triggers.Should().ContainSingle(static trigger => trigger.Target == TriggerTarget.Room && trigger.Room == "Hall");
        tool.Sections.Select(static section => section.Title).Should().Equal("Shot", "Triggers", "Picked trigger", "This shot", "Building");
        tool.Sections[1].Rows.OfType<ListRow>().Single().Label.Should().Be("Room: Hall");
    }

    [Fact]
    public void SetFromViewPutsTheShotWhereTheViewIsAndLookThroughHandsItOn()
    {
        BuildingToolViewModel tool = _builder.Build();
        _builder.Editor.Tool = BuildingTool.Camera;
        _builder.Editor.DropShot(new Vector2(0, 0));
        ActionsRow actions = tool.Sections[0].Rows.OfType<ActionsRow>().Single();

        actions.Actions.Single(static action => action.Name == "Set from view").Command.Execute(null);
        actions = tool.Sections[0].Rows.OfType<ActionsRow>().Single();
        actions.Actions.Single(static action => action.Name == "Look through").Command.Execute(null);

        _builder.Load().Shots.Single().Position.Should().Be(new Vector3(1, 8, -9));
        _builder.LookedThrough.Should().ContainSingle(static shot => shot.Name == "Hall shot");
    }
}
