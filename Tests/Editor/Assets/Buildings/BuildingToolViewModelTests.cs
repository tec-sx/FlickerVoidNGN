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
    public void TheArcToolShowsHowFarAWallBulgesAndToWhichSideWithItsChordAndItsLength()
    {
        BuildingToolViewModel tool = _builder.Build();
        _builder.Editor.Tool = BuildingTool.Arc;
        _builder.Editor.Pick(new PlanPick(PlanPickKind.Wall, 0, FrontWall));

        ((NumberRow)Section(tool, "Arc").Rows[0]).Value = 0.9;
        float outward = _builder.Document.Building.Storeys[0].Walls.Single(static wall => wall.Id == FrontWall).Bulge;
        ((ChoiceRow)Section(tool, "Arc").Rows[2]).Options[1].ChooseCommand.Execute(null);
        float inward = _builder.Document.Building.Storeys[0].Walls.Single(static wall => wall.Id == FrontWall).Bulge;

        Section(tool, "Arc").Rows.Select(static row => row.Label).Should().StartWith(["Bulge", "Segments", "Side", "Chord", "Along the arc"]);
        outward.Should().BeApproximately(-0.9f, 1e-4f, "a new bulge goes out of the wall's outside");
        inward.Should().BeApproximately(0.9f, 1e-4f);
        ((NumberRow)Section(tool, "Arc").Rows[0]).Value.Should().BeApproximately(0.9, 1e-4, "the reach is shown without its side");
        double chord = double.Parse(((ListRow)Section(tool, "Arc").Rows[3]).Value.Replace(" m", string.Empty), System.Globalization.CultureInfo.InvariantCulture);
        double along = double.Parse(((ListRow)Section(tool, "Arc").Rows[4]).Value.Replace(" m", string.Empty), System.Globalization.CultureInfo.InvariantCulture);
        along.Should().BeGreaterThan(chord);
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
        tool.Sections.Select(static section => section.Title).Should().Equal("Opening", "Place", "Size", "Leaf", "Handle", "Frame and window", "Cut");
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

    [Fact]
    public void ARowThatWritesBackAsItsSectionGoesNeitherEditsTheBuildingNorDoublesTheSections()
    {
        BuildingToolViewModel tool = _builder.Build();
        _builder.Editor.Tool = BuildingTool.Opening;
        _builder.Editor.Pick(new PlanPick(PlanPickKind.Opening, 0, FrontDoor));
        ToolSection place = tool.Sections.Single(static section => section.Title == "Place");
        NumberRow along = (NumberRow)place.Rows[0];
        float before = _builder.Document.Building.Storeys[0].Openings.Single().Along;

        // A number box that held the keyboard commits its rounded text as it leaves the tab.
        tool.Sections.CollectionChanged += (_, _) =>
        {
            if (!tool.Sections.Contains(place))
            {
                along.Value = before + 0.5;
            }
        };
        _builder.Editor.Pick(null);

        _builder.Document.Building.Storeys[0].Openings.Single().Along.Should().Be(before);
        tool.IsPending.Should().BeFalse();
        tool.Sections.Select(static section => section.Title).Should().Equal("New opening", "Storey: Ground");
    }

    [Fact]
    public void ANumberTakenInPlaceTakesItsLimitsToo()
    {
        NumberRow shown = new("Along the wall", 1, "m", static _ => { }, 0, 4);
        NumberRow next = new("Along the wall", 6, "m", static _ => { }, 0, 9);

        shown.Take(next);

        shown.Maximum.Should().Be(9);
        shown.Value.Should().Be(6);
    }

    [Fact]
    public void ADoorShowsItsLeavesItsHandleAndTheWindowInItsLeaf()
    {
        BuildingToolViewModel tool = _builder.Build();

        _builder.Editor.Pick(new PlanPick(PlanPickKind.Opening, 0, FrontDoor));

        tool.Sections.Select(static section => section.Title).Should().Equal("Opening", "Place", "Size", "Leaf", "Handle", "Frame and window", "Cut");
        Section(tool, "Leaf").Rows.Select(static row => row.Label).Should().StartWith(["Leaves", "Hinge", "Opens", "Angle"]);
        Section(tool, "Handle").Rows.Select(static row => row.Label).Should().Equal("Height", "Inset");
        Section(tool, "Frame and window").Rows.Select(static row => row.Label).Should().Equal("Frame width", "Window");
    }

    [Fact]
    public void ADoubleDoorHasNoHingeSideAndTheSwitchMakesTwoLeaves()
    {
        BuildingToolViewModel tool = _builder.Build();
        _builder.Editor.Pick(new PlanPick(PlanPickKind.Opening, 0, FrontDoor));
        ChoiceRow leaves = (ChoiceRow)Section(tool, "Leaf").Rows[0];

        leaves.Options[1].ChooseCommand.Execute(null);

        _builder.Document.Building.Storeys[0].Openings.Single().Double.Should().BeTrue();
        Section(tool, "Leaf").Rows.Select(static row => row.Label).Should().StartWith(["Leaves", "Opens", "Angle"]);
    }

    [Fact]
    public void TheWindowSwitchCutsAWindowInTheLeafAndShowsItsSizeAndGlass()
    {
        BuildingToolViewModel tool = _builder.Build();
        _builder.Editor.Pick(new PlanPick(PlanPickKind.Opening, 0, FrontDoor));
        ToggleRow window = (ToggleRow)Section(tool, "Frame and window").Rows[1];

        window.IsOn = true;

        PlanOpening door = _builder.Document.Building.Storeys[0].Openings.Single();
        door.WindowWidth.Should().BeGreaterThan(0);
        Section(tool, "Frame and window").Rows.Select(static row => row.Label).Should().Equal("Frame width", "Window", "Width", "Height", "From the floor", "Glass");
        ((ToggleRow)Section(tool, "Frame and window").Rows[5]).IsOn.Should().BeTrue("a window is a pane to see through until it is switched off");
    }

    [Fact]
    public void APickedHandleShowsItsDoorWithTheHandleOpen()
    {
        BuildingToolViewModel tool = _builder.Build();

        _builder.Editor.Pick(new PlanPick(PlanPickKind.Handle, 0, FrontDoor));
        NumberRow height = (NumberRow)Section(tool, "Handle").Rows[0];
        height.Value = 1.25;

        tool.Subtitle.Should().Be("Handle of Front door, Ground");
        Section(tool, "Handle").IsExpanded.Should().BeTrue();
        _builder.Document.Building.Storeys[0].Openings.Single().HandleHeight.Should().BeApproximately(1.25f, 1e-4f);
    }

    [Fact]
    public void PickingTheHandleOfADoorAlreadyShownOpensItsHandleSection()
    {
        BuildingToolViewModel tool = _builder.Build();
        _builder.Editor.Pick(new PlanPick(PlanPickKind.Opening, 0, FrontDoor));
        bool before = Section(tool, "Handle").IsExpanded;

        _builder.Editor.Pick(new PlanPick(PlanPickKind.Handle, 0, FrontDoor));

        before.Should().BeFalse();
        Section(tool, "Handle").IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void PickedStairsShowTheirRunsTheirStepsTheOpeningAboveAndTheirCamera()
    {
        _builder.Document.SetStoreys(2, false);
        int id = 0;
        _builder.Document.Change(building => id = BuildingTools.AddStairs(building, 0, new Vector2(-1, -2), 0, StairsKind.Straight)!.Value);
        BuildingToolViewModel tool = _builder.Build();

        _builder.Editor.Pick(new PlanPick(PlanPickKind.Stairs, 0, id));

        tool.Sections.Select(static section => section.Title).Should().Equal("Stairs", "Steps", "Opening above", "Camera");
        Section(tool, "Steps").Rows.Select(static row => row.Label).Should().Equal("Rise", "Tread", "Count", "Meets the floor");
        Section(tool, "Camera").Rows[0].Label.Should().Be("While on the stairs");
    }

    [Fact]
    public void TheWallToolChoosesWhatItsEndsSnapTo()
    {
        BuildingToolViewModel tool = _builder.Build();

        _builder.Editor.Tool = BuildingTool.Wall;
        ToolSection snaps = Section(tool, "Snaps to");
        ((ToggleRow)snaps.Rows[1]).IsOn = false;

        snaps.Rows.Select(static row => row.Label).Should().Equal("Walls, corners", "Wall middles", "Grid");
        _builder.Editor.SnapToMiddles.Should().BeFalse();
    }

    private static ToolSection Section(BuildingToolViewModel tool, string title) => tool.Sections.First(section => section.Title == title);
}
