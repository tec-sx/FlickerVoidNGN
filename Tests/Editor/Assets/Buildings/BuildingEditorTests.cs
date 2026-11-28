using System.Numerics;

using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Editor.Core.Gizmos;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Plans;

namespace FlickerVoid.Editor.Assets.Tests.Buildings;

public sealed class BuildingEditorTests : IDisposable
{
    private const int FrontWall = 5;
    private const int FrontDoor = 9;

    private readonly BuildingEditorTestsBuilder _builder = new();

    /// <summary>How far snapping to the grid takes a dragged point from the pointer, at most.</summary>
    private const float GridStep = 0.051f;

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AClickOnAWallsFaceHighUpPicksItAndTheDragPushesItByWhatTheMouseMoved()
    {
        BuildingEditor editor = _builder.Editor;
        editor.Tool = BuildingTool.PushWall;
        float height = _builder.Floor + 2;

        bool taken = _builder.Press(new Vector3(2.5f, height, -2.7f));
        _builder.Drag(new Vector3(2.5f, height, -3.7f));
        _builder.Release();

        taken.Should().BeTrue();
        editor.Picked.Should().Be(new PlanPick(PlanPickKind.Wall, 0, FrontWall));
        editor.Pushed.Should().BeApproximately(1, 1e-3f);
        _builder.Building.Storeys[0].Corners[0].Z.Should().BeApproximately(-3.85f, 1e-3f);
        editor.IsPending.Should().BeTrue();
    }

    [Fact]
    public void AWallDraggedWithTheArcToolBulgesOutOfTheBuildingAndTheEditIsNamedByItsSide()
    {
        BuildingEditor editor = _builder.Editor;
        editor.Tool = BuildingTool.Arc;
        float height = _builder.Floor + 2;

        bool taken = _builder.Press(new Vector3(0, height, -2.7f));
        _builder.Drag(new Vector3(0, height, -3.4f));
        _builder.Release();

        taken.Should().BeTrue();
        PlanWall front = _builder.Building.Storeys[0].Walls.Single(wall => wall.Id == FrontWall);
        front.Bulge.Should().BeLessThan(0, "a wall dragged away from its rooms bulges out");
        editor.PendingName.Should().EndWith("m out", "the edit waiting is named as the Tool tab shows it, by a length and a side");
        editor.PendingName.Should().NotContain("-");
    }

    [Fact]
    public void ThePushArrowOfThePickedWallPushesItWhenDragged()
    {
        BuildingEditor editor = _builder.Editor;
        editor.Tool = BuildingTool.PushWall;
        editor.Pick(new PlanPick(PlanPickKind.Wall, 0, FrontWall));
        float floor = _builder.Floor + 0.02f;

        // The arrow stands out of the wall's outer face at the floor; it is taken a little way along it.
        bool taken = _builder.Press(new Vector3(0, floor, -3.25f));
        _builder.Drag(new Vector3(0, floor, -3.75f));
        _builder.Release();

        taken.Should().BeTrue();
        editor.Pushed.Should().BeApproximately(0.5f, 1e-3f);
        _builder.Building.Storeys[0].Corners[0].Z.Should().BeApproximately(-3.35f, 1e-3f);
    }

    [Fact]
    public void TheSelectToolPicksPlacedStairsByAClickOnTheirSteps()
    {
        _builder.WithStoreys(2);
        int id = 0;
        _builder.Document.Change(building => id = BuildingTools.AddStairs(building, 0, new Vector2(-1, -2), 0, StairsKind.Straight)!.Value);

        _builder.Press(new Vector3(-1, _builder.Floor + 1.2f, 0.2f));

        _builder.Editor.Picked.Should().Be(new PlanPick(PlanPickKind.Stairs, 0, id));
    }

    [Fact]
    public void StairsWithNoStoreyAboveAreNotPlacedAndTheUserIsToldWhy()
    {
        BuildingEditor editor = _builder.Editor;
        editor.Tool = BuildingTool.Stairs;

        bool taken = _builder.Press(new Vector3(0, _builder.Floor, 0));

        taken.Should().BeTrue("the click was the tool's, so no part is picked under it");
        _builder.Warnings.Should().ContainSingle().Which.Should().Contain("storey above");
        _builder.Building.Stairs.Should().BeEmpty();
        editor.IsPending.Should().BeFalse();
    }

    [Fact]
    public void NewStairsAreTurnedTheWayTheyAreDraggedFromTheirFoot()
    {
        _builder.WithStoreys(2);
        BuildingEditor editor = _builder.Editor;
        editor.Tool = BuildingTool.Stairs;

        _builder.Press(new Vector3(-2, _builder.Floor, 0));
        _builder.Drag(new Vector3(0, _builder.Floor, 0));
        _builder.Release();

        BuildingStairs stairs = _builder.Building.Stairs.Single();
        stairs.X.Should().BeApproximately(-2, 1e-3f);
        stairs.Heading.Should().BeApproximately(90, 1e-3f, "they climb toward +X, where the mouse went");
    }

    [Fact]
    public void AnOpeningOnNoWallIsNotPlacedAndTheUserIsToldWhy()
    {
        BuildingEditor editor = _builder.Editor;
        editor.Tool = BuildingTool.Opening;
        editor.NewOpening = OpeningKind.Window;
        _builder.LookDown();

        _builder.Press(new Vector3(0, _builder.Floor, 0));

        _builder.Warnings.Should().ContainSingle().Which.Should().Contain("wall");
        _builder.Building.Storeys[0].Openings.Should().ContainSingle("only the front door is there");
    }

    [Fact]
    public void ADoorDraggedPastItsWallsEndStopsAtTheEnd()
    {
        BuildingEditor editor = _builder.Editor;
        editor.Tool = BuildingTool.Opening;
        float height = _builder.Floor + 1;

        _builder.Press(new Vector3(0, height, -2.7f));
        _builder.Drag(new Vector3(-3, height, -2.7f));
        _builder.Drag(new Vector3(-9, height, -2.7f));
        _builder.Release();

        PlanOpening door = _builder.Building.Storeys[0].Openings.Single();
        editor.Picked.Should().Be(new PlanPick(PlanPickKind.Opening, 0, FrontDoor));
        door.Wall.Should().Be(FrontWall);
        OpeningShape.Fits(_builder.Building, _builder.Building.Storeys[0], door).Should().BeTrue();
    }

    [Fact]
    public void AnOpeningDraggedOntoAnotherIsShownNotToFitAndTheUserIsToldWhy()
    {
        BuildingEditor editor = _builder.Editor;
        int id = 0;
        _builder.Document.Change(building => id = PlanEdits.AddOpening(building.Storeys[0], FrontWall, 1.2f, OpeningKind.Window));
        editor.Tool = BuildingTool.Opening;
        PlanOpening door = _builder.Building.Storeys[0].Openings.Single(opening => opening.Id == FrontDoor);
        OpeningShape.TryGetFrame(_builder.Building, _builder.Building.Storeys[0], door, out OpeningFrame frame);
        PlanOpening window = _builder.Building.Storeys[0].Openings.Single(opening => opening.Id == id);
        Vector3 onWindow = frame.Origin + (frame.Along * (window.Along - door.Along)) + (Vector3.UnitY * 1.3f) - (frame.Out * 0.15f);
        Vector3 onDoor = frame.Origin + (Vector3.UnitY * 1.3f) - (frame.Out * 0.15f);

        _builder.Press(onWindow);
        _builder.Drag(Vector3.Lerp(onWindow, onDoor, 0.5f));
        _builder.Drag(onDoor);
        IReadOnlyList<PlanLabel> labels = editor.GetLabels();
        _builder.Release();

        editor.Picked.Should().Be(new PlanPick(PlanPickKind.Opening, 0, id));
        labels.Should().Contain(label => label.Detail == "runs into Front door");
        _builder.Warnings.Should().ContainSingle().Which.Should().Contain("runs into Front door");
    }

    [Fact]
    public void ADoorsHandleIsPickedByAClickOnItAndMovedByDraggingIt()
    {
        BuildingEditor editor = _builder.Editor;
        editor.Pick(new PlanPick(PlanPickKind.Opening, 0, FrontDoor));
        BuildingStorey ground = _builder.Building.Storeys[0];
        PlanOpening door = ground.Openings.Single();
        OpeningShape.TryGetFrame(_builder.Building, ground, door, out OpeningFrame frame);
        DoorLeaf leaf = OpeningShape.MakeLeaf(frame, door);
        Vector3 handle = leaf.Pivot + leaf.Handle;

        bool taken = _builder.Press(handle);
        _builder.Drag(handle + new Vector3(0, 0.2f, 0));
        _builder.Release();

        taken.Should().BeTrue();
        editor.Picked.Should().Be(new PlanPick(PlanPickKind.Handle, 0, FrontDoor));
        _builder.Building.Storeys[0].Openings.Single().HandleHeight.Should().BeApproximately(1.2f, 0.051f);
    }

    [Fact]
    public void APickedDoorIsTaggedWithItsHingeAndItsHandleInAnyTool()
    {
        BuildingEditor editor = _builder.Editor;
        IReadOnlyList<PlanLabel> before = editor.GetLabels();

        editor.Pick(new PlanPick(PlanPickKind.Opening, 0, FrontDoor));
        IReadOnlyList<PlanLabel> door = editor.GetLabels();
        editor.Pick(new PlanPick(PlanPickKind.Handle, 0, FrontDoor));
        IReadOnlyList<PlanLabel> handle = editor.GetLabels();

        before.Should().NotContain(label => label.Title.StartsWith("Hinge"));
        door.Should().ContainSingle(label => label.Title == "Hinge").Which.Detail.Should().StartWith("opens out");
        door.Should().ContainSingle(label => label.Title == "Handle").Which.Detail.Should().Be("drag to move");
        handle.Should().ContainSingle(label => label.Kind == PlanLabelKind.Drag).Which.Title.Should().Be("Handle 1.00 m up");
    }

    [Fact]
    public void TakingAToolKeepsWhatIsPickedOnlyWhenTheToolWorksOnIt()
    {
        BuildingEditor editor = _builder.Editor;
        editor.Pick(new PlanPick(PlanPickKind.Opening, 0, FrontDoor));

        editor.Tool = BuildingTool.Opening;
        PlanPick? withItsTool = editor.Picked;
        editor.Tool = BuildingTool.Stairs;

        withItsTool.Should().Be(new PlanPick(PlanPickKind.Opening, 0, FrontDoor));
        editor.Picked.Should().BeNull("a door is not the stairs tool's to edit");
    }

    [Fact]
    public void TheRoofToolShowsTheBuildingWholeWithNothingOfAStoreyDrawnThroughIt()
    {
        BuildingEditor editor = _builder.Editor;
        OverlayBuilder overlay = new();
        bool before = editor.ShowsWhole;

        editor.Tool = BuildingTool.Roof;
        overlay.Begin(_builder.Camera, BuildingEditorTestsBuilder.Height);
        editor.Draw(overlay);

        before.Should().BeFalse();
        editor.ShowsWhole.Should().BeTrue();
        overlay.Vertices.Length.Should().Be(0);
        editor.GetLabels().Should().BeEmpty("the rooms' names would show through the roof");
    }

    [Fact]
    public void ChoosingAGizmoModeTakesTheSelectToolAndAPlanToolPutsTheGizmoAway()
    {
        BuildingEditor editor = _builder.Editor;
        editor.Tool = BuildingTool.PushWall;

        editor.GizmoMode = GizmoMode.Rotate;
        BuildingTool withGizmo = editor.Tool;
        editor.Tool = BuildingTool.Stairs;

        withGizmo.Should().Be(BuildingTool.Select);
        editor.GizmoMode.Should().Be(GizmoMode.Select);
    }

    [Fact]
    public void TheRotateGizmoTurnsAPickedElevator()
    {
        _builder.WithStoreys(2);
        int id = 0;
        _builder.Document.Change(building => id = BuildingTools.AddElevator(building, new Vector2(1, 0), 0));
        BuildingEditor editor = _builder.Editor;
        editor.Pick(new PlanPick(PlanPickKind.Elevator, 0, id));
        editor.GizmoMode = GizmoMode.Rotate;
        Vector3 centre = new(1, _builder.Floor, 0);
        float reach = editor.Gizmo.GetHandleLength(_builder.Camera, centre, BuildingEditorTestsBuilder.Height);

        // Along the ring that turns about the upright axis, from 30 degrees round it to 75.
        bool taken = _builder.Press(centre + (OnRing(30) * reach));
        _builder.Drag(centre + (OnRing(75) * reach));
        _builder.Release();

        taken.Should().BeTrue();
        MathF.Abs(_builder.Building.Elevators.Single().Heading).Should().BeApproximately(45, 1e-3f);
        editor.IsPending.Should().BeTrue("a gizmo's edit waits for Apply like any other");
    }

    [Fact]
    public void NothingBehindTheGizmoIsHoveredWhileThePointerIsOnIt()
    {
        _builder.WithStoreys(2);
        int id = 0;
        _builder.Document.Change(building => id = BuildingTools.AddElevator(building, new Vector2(1, 0), 0));
        BuildingEditor editor = _builder.Editor;
        editor.Pick(new PlanPick(PlanPickKind.Elevator, 0, id));
        Vector3 centre = new(1, _builder.Floor, 0);
        float reach = editor.Gizmo.GetHandleLength(_builder.Camera, centre, BuildingEditorTestsBuilder.Height);
        Vector3 onRing = centre + (OnRing(30) * reach);

        _builder.Drag(onRing);
        PlanPick? without = editor.Hover;
        editor.GizmoMode = GizmoMode.Rotate;
        _builder.Drag(onRing);

        without.Should().NotBeNull("with no gizmo, a click there picks what stands there");
        editor.Gizmo.Hovered.Should().Be(GizmoAxis.Y);
        editor.Hover.Should().BeNull("a press there takes the ring");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACornerFollowsThePointerFromWhereItWasGrabbedAtItsFootOrItsHead(bool byHead)
    {
        BuildingEditor editor = _builder.Editor;
        editor.Tool = BuildingTool.MoveCorner;
        BuildingStorey ground = _builder.Building.Storeys[0];
        PlanCorner corner = ground.Corners[0];
        Vector2 before = new(corner.X, corner.Z);
        float height = _builder.Floor;
        if (byHead)
        {
            height += ground.Height - ground.Floor;
        }

        Vector3 grabbed = new(before.X, height, before.Y);
        bool taken = _builder.Press(grabbed);
        _builder.Drag(grabbed + new Vector3(0.5f, 0, 0.2f));
        _builder.Release();

        taken.Should().BeTrue();
        PlanCorner moved = _builder.Building.Storeys[0].Corners[0];
        moved.X.Should().BeApproximately(before.X + 0.5f, GridStep, "the corner moves by what the pointer moved, at the height it was taken");
        moved.Z.Should().BeApproximately(before.Y + 0.2f, GridStep);
    }

    [Fact]
    public void NothingStaysHoveredOnceThePointerLeavesTheViewport()
    {
        int id = 0;
        _builder.Document.Change(building => id = PlanEdits.AddPillar(building.Storeys[0], new Vector2(1, 1), 0.4f, false));
        BuildingEditor editor = _builder.Editor;
        editor.Tool = BuildingTool.Pillar;
        Vector3 onPillar = new(1, _builder.Floor + 1, 1);
        _builder.Drag(onPillar);
        PlanPick? over = editor.Hover;
        int changes = 0;
        editor.Changed += () => changes++;

        editor.Leave();

        over.Should().NotBeNull();
        editor.Hover.Should().BeNull();
        changes.Should().Be(1, "the overlay is drawn again without the hover");
    }

    [Fact]
    public void TheMoveGizmoSlidesAPickedPillarAlongItsArrow()
    {
        int id = 0;
        _builder.Document.Change(building => id = PlanEdits.AddPillar(building.Storeys[0], new Vector2(1, 1), 0.4f, false));
        BuildingEditor editor = _builder.Editor;
        editor.Pick(new PlanPick(PlanPickKind.Pillar, 0, id));
        editor.GizmoMode = GizmoMode.Move;
        BuildingStorey ground = _builder.Building.Storeys[0];
        Vector3 centre = new(1, _builder.Floor + ((ground.Height - ground.Floor) / 2), 1);
        float reach = editor.Gizmo.GetHandleLength(_builder.Camera, centre, BuildingEditorTestsBuilder.Height);

        bool taken = _builder.Press(centre + (Vector3.UnitX * (reach * 0.6f)));
        _builder.Drag(centre + (Vector3.UnitX * ((reach * 0.6f) + 1)));
        _builder.Release();

        taken.Should().BeTrue();
        PlanPillar pillar = _builder.Building.Storeys[0].Pillars.Single();
        pillar.X.Should().BeApproximately(2, 1e-3f);
        pillar.Z.Should().BeApproximately(1, 1e-3f);
    }

    [Fact]
    public void AWallDrawnToNearAnotherJoinsItAndSaysSoWhileDragged()
    {
        BuildingEditor editor = _builder.Editor;
        editor.Tool = BuildingTool.Wall;
        float floor = _builder.Floor;

        _builder.Press(new Vector3(1, floor, 0));
        _builder.Drag(new Vector3(1, floor, 2.6f));
        IReadOnlyList<PlanLabel> labels = editor.GetLabels();
        _builder.Release();

        labels.Should().Contain(label => label.Detail.StartsWith("Joins"));
        BuildingStorey ground = _builder.Building.Storeys[0];
        ground.Corners.Should().Contain(corner => MathF.Abs(corner.X - 1) < 1e-3f && MathF.Abs(corner.Z - 2.85f) < 1e-3f, "the end lies on the back wall's line");
    }

    [Fact]
    public void DrawsEveryToolOverABuildingWithADoubleDoorStairsAnElevatorAndAShot()
    {
        _builder.WithStoreys(2);
        _builder.Document.Change(building =>
        {
            PlanOpening door = building.Storeys[0].Openings.Single();
            door.Double = true;
            door.WindowWidth = 0.3f;
            BuildingTools.AddStairs(building, 0, new Vector2(-2, -1), 0, StairsKind.L);
            BuildingTools.AddElevator(building, new Vector2(2, 1), 30);
            PlanEdits.AddPillar(building.Storeys[0], new Vector2(0, 1), 0.4f, true);
        });
        BuildingEditor editor = _builder.Editor;
        OverlayBuilder overlay = new();
        PlanPick[] picks =
        [
            new(PlanPickKind.Wall, 0, FrontWall),
            new(PlanPickKind.Opening, 0, FrontDoor),
            new(PlanPickKind.Handle, 0, FrontDoor),
            new(PlanPickKind.Stairs, 0, 1),
            new(PlanPickKind.Elevator, 0, 1),
            new(PlanPickKind.Room, 0, 0, "Hall"),
            new(PlanPickKind.Corner, 0, 1),
        ];

        foreach (BuildingTool tool in Enum.GetValues<BuildingTool>())
        {
            editor.Tool = tool;
            foreach (PlanPick pick in picks)
            {
                editor.Pick(pick);
                overlay.Begin(_builder.Camera, BuildingEditorTestsBuilder.Height);
                editor.Draw(overlay);
                overlay.Vertices.Length.Should().BeGreaterThan(0, $"{tool} shows the picked {pick.Kind}");
                editor.GetLabels().Should().NotBeNull();
            }
        }
    }

    private static Vector3 OnRing(float degrees)
    {
        float angle = float.DegreesToRadians(degrees);
        return new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
    }
}
