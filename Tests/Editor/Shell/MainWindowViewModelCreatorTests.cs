using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Assets.Buildings;
using FlickerVoid.Editor.Assets.Foliage;
using FlickerVoid.Editor.Assets.Vehicles;
using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Editor.Viewports;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Foliage;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Greyboxes;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Tests;

/// <summary>The vehicle creator in the editor's window: entering and leaving it, its panel, and what undo, redo and save act on.</summary>
public sealed class MainWindowViewModelCreatorTests : IDisposable
{
    private readonly MainWindowViewModelTestsBuilder _builder = new();

    public MainWindowViewModelCreatorTests()
    {
        _builder.OpenStreet();
    }

    private MainWindowViewModel ViewModel => _builder.ViewModel;

    private ViewportViewModel Viewport => _builder.ViewModel.Viewport;

    private VehiclePanelViewModel VehiclePanel => (VehiclePanelViewModel)_builder.ViewModel.CreatorPanel!;

    public void Dispose() => _builder.Dispose();

    private bool IsHidden(string part)
    {
        CreationParts parts = _builder.Scene.Game.Services.GetService<CreationParts>()!;
        Entity entity = parts.Find(Viewport.Creator!.Root, part);
        return _builder.Scene.IsHidden(entity);
    }

    [Fact]
    public void DoubleClickingAPlacedVehicleEditsItInPlace()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));

        CreatorMode? creator = Viewport.Creator;
        creator.Should().NotBeNull();
        creator!.Placement.Should().Be(CreatorPlacement.InPlace);
        creator.Root.Should().Be(_builder.Find("Van"));
        Viewport.Mode.Should().BeSameAs(creator);
        Viewport.CreatorFile.Should().Be(MainWindowViewModelTestsBuilder.Van);
        ViewModel.CreatorPanel.Should().NotBeNull();
        VehiclePanel.Type.Should().Be(VehicleType.Car);
        ViewModel.SideTab.Should().Be(MainWindowViewModel.CreatorTab, "the creator's tools come in front");
        ViewModel.ShowsInspector.Should().BeFalse("nothing in the level is selected");
        ViewModel.OutlinerTab.Should().Be(MainWindowViewModel.PartsTab, "the parts take the scene's place");
        ViewModel.CreatorParts.Should().Equal("body", "wheel.1L", "wheel.1R", "wheel.2L", "wheel.2R");
        ViewModel.CreatorHeading.Should().Be(MainWindowViewModelTestsBuilder.Van);
    }

    [Fact]
    public void TheEditMeshButtonShowsInTheCreatorAndEntersTheSelectedPartsMesh()
    {
        MeshToolsViewModel tools = Viewport.MeshTools;
        bool shownInTheScene = tools.IsCreating;
        _builder.DoubleClick(_builder.MiddleOf("Van"));
        ViewModel.CreatorPart = "body";

        tools.ToggleMesh();

        shownInTheScene.Should().BeFalse();
        tools.IsCreating.Should().BeTrue();
        tools.IsMeshMode.Should().BeTrue();
        Viewport.Creator!.MeshPart.Should().Be("body");
        tools.ToggleMesh();
        tools.IsMeshMode.Should().BeFalse("pressing it again leaves the mesh");
    }

    [Fact]
    public void TabEntersAndLeavesTheSelectedPartsMesh()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));
        ViewModel.CreatorPart = "body";

        Viewport.KeyPressed(ViewportKey.Tab, ViewportButtons.None);

        Viewport.MeshTools.IsMeshMode.Should().BeTrue();
        Viewport.KeyPressed(ViewportKey.Tab, ViewportButtons.None);
        Viewport.MeshTools.IsMeshMode.Should().BeFalse();
    }

    [Fact]
    public void EditingTheMeshWithNoPartSelectedSaysWhatToClick()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));

        Viewport.MeshTools.ToggleMesh();

        Viewport.MeshTools.IsMeshMode.Should().BeFalse();
        Viewport.Status.Should().Contain("Click a part");
    }

    [Fact]
    public void TheMeshToolsJoinTheRailAndTheToolTabComesInFrontOnlyInTheMesh()
    {
        EnterBodyWithAFaceSelected();
        List<string> tools = [.. Viewport.ToolRail.Groups.SelectMany(group => group.Items).Select(item => item.Name)];

        ViewModel.ShowsTool.Should().BeTrue();
        ViewModel.SideTab.Should().Be(MainWindowViewModel.ToolTab);
        tools.Should().Contain(["Extrude", "Inset", "Bevel", "Loop cut", "Cut", "Door", "Mirror"]);
        Viewport.MeshTools.Leave();
        ViewModel.ShowsTool.Should().BeFalse();
        List<string> outside = [.. Viewport.ToolRail.Groups.SelectMany(group => group.Items).Select(item => item.Name)];
        outside.Should().Equal("Select", "Move", "Rotate", "Scale", "Door");
    }

    [Fact]
    public void TheDoorToolIsLitInTheRailAndOpensTheToolTabUntilItIsCancelled()
    {
        Assert.SkipUnless(FlickerVoid.Editor.Core.Meshes.Doors.DoorFitter.IsAvailable, "Manifold's library is not built.");
        EnterBodyWithAFaceSelected();
        Viewport.MeshTools.Leave();

        Viewport.KeyPressed(ViewportKey.D, ViewportButtons.None);

        Viewport.MeshTools.IsDoorTool.Should().BeTrue();
        Viewport.MeshTools.IsConfirming.Should().BeTrue("Apply and Cancel wait at the bottom");
        ViewModel.ShowsTool.Should().BeTrue();
        Viewport.ToolRail.Groups.SelectMany(group => group.Items).Single(item => item.Name == "Door").IsActive.Should().BeTrue();
        Viewport.KeyPressed(ViewportKey.Escape, ViewportButtons.None);
        Viewport.MeshTools.IsDoorTool.Should().BeFalse();
        ViewModel.ShowsTool.Should().BeFalse();
    }

    [Fact]
    public void AToolInHandIsLitInTheRailAndApplyKeepsItsWork()
    {
        MeshToolsViewModel tools = EnterBodyWithAFaceSelected();
        int points = ViewModel.CreatorPanel!.FindMesh("body")!.Points.Count;

        Viewport.KeyPressed(ViewportKey.X, ViewportButtons.None);
        Viewport.PointerMoved(new Vector2(300, 260), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);
        Viewport.PointerMoved(new Vector2(300, 200), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);
        bool lit = Viewport.ToolRail.Groups.SelectMany(group => group.Items).Single(item => item.IsActive && item.Name == "Extrude") is not null;
        bool asking = tools.ShowsAmount && !tools.ShowsResting;
        tools.ApplyCommand.Execute(null);

        lit.Should().BeTrue();
        asking.Should().BeTrue("the Tool tab shows the extrude's amount while it is held");
        tools.IsDragging.Should().BeFalse();
        ViewModel.CreatorPanel.FindMesh("body")!.Points.Should().HaveCount(points + 4);
    }

    [Fact]
    public void CancelPutsTheMeshBack()
    {
        MeshToolsViewModel tools = EnterBodyWithAFaceSelected();
        int points = ViewModel.CreatorPanel!.FindMesh("body")!.Points.Count;
        Viewport.KeyPressed(ViewportKey.X, ViewportButtons.None);
        Viewport.PointerMoved(new Vector2(300, 200), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);

        tools.CancelCommand.Execute(null);

        ViewModel.CreatorPanel.FindMesh("body")!.Points.Should().HaveCount(points);
        tools.IsDragging.Should().BeFalse();
    }

    [Fact]
    public void ExtrudeFollowsTheMouseUntilAClickSetsItAndEnterKeepsIt()
    {
        MeshToolsViewModel tools = EnterBodyWithAFaceSelected();
        int points = ViewModel.CreatorPanel!.FindMesh("body")!.Points.Count;
        Viewport.PointerMoved(new Vector2(300, 300), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);

        Viewport.KeyPressed(ViewportKey.X, ViewportButtons.None);
        Viewport.PointerMoved(new Vector2(300, 260), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);
        Viewport.PointerMoved(new Vector2(300, 200), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);
        bool dragging = tools.IsDragging;
        Viewport.PointerPressed(new Vector2(300, 200), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height, additive: false, loop: false);
        bool pending = tools.IsDragging;
        Viewport.KeyPressed(ViewportKey.Enter, ViewportButtons.None);

        dragging.Should().BeTrue();
        pending.Should().BeTrue("a click only sets the tool");
        tools.IsDragging.Should().BeFalse();
        tools.Amount.Should().BeGreaterThan(0);
        ViewModel.CreatorPanel.FindMesh("body")!.Points.Should().HaveCount(points + 4);
        Viewport.Creator!.MeshSelection!.Faces.Should().ContainSingle("Enter kept the tool's work and the click selected nothing else");
        Viewport.Mode.History.Undo();
        ViewModel.CreatorPanel.FindMesh("body")!.Points.Should().HaveCount(points, "the whole drag is one undo step");
    }

    [Fact]
    public void EscapeCancelsADraggedToolAndStaysInTheMesh()
    {
        MeshToolsViewModel tools = EnterBodyWithAFaceSelected();
        int faces = ViewModel.CreatorPanel!.FindMesh("body")!.Faces.Count;
        Viewport.PointerMoved(new Vector2(300, 300), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);

        Viewport.KeyPressed(ViewportKey.I, ViewportButtons.None);
        Viewport.PointerMoved(new Vector2(300, 280), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);
        int shown = ViewModel.CreatorPanel.FindMesh("body")!.Faces.Count;
        Viewport.KeyPressed(ViewportKey.Escape, ViewportButtons.None);

        shown.Should().Be(faces + 4);
        ViewModel.CreatorPanel.FindMesh("body")!.Faces.Should().HaveCount(faces);
        tools.IsMeshMode.Should().BeTrue();
        Viewport.Mode.History.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void ANumberTypedWhileAToolIsHeldSetsItsAmountAndTheMouseLetsGo()
    {
        MeshToolsViewModel tools = EnterBodyWithAFaceSelected();
        Viewport.KeyPressed(ViewportKey.X, ViewportButtons.None);

        tools.Amount = 0.75;
        Viewport.PointerMoved(new Vector2(300, 100), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);

        tools.Amount.Should().Be(0.75);
        tools.IsDragging.Should().BeTrue();
    }

    [Fact]
    public void TheKeys123PickPointsEdgesAndFacesAndTheBarFollows()
    {
        MeshToolsViewModel tools = EnterBodyWithAFaceSelected();
        List<string?> changed = [];
        tools.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Viewport.KeyPressed(ViewportKey.Two, ViewportButtons.None);

        tools.IsEdge.Should().BeTrue();
        changed.Should().Contain([nameof(MeshToolsViewModel.IsEdge), nameof(MeshToolsViewModel.IsFace)]);
        Viewport.KeyPressed(ViewportKey.One, ViewportButtons.None);
        tools.IsPoint.Should().BeTrue();
    }

    [Fact]
    public void TheMeshMenuHoldsTheToolsAndTheSwitchesAsTheyAreSet()
    {
        MeshToolsViewModel tools = EnterBodyWithAFaceSelected();

        IReadOnlyList<MenuChoice> menu = tools.GetMenu();
        menu.Single(choice => choice.Name == "Cut Through: Off").Run();
        menu.Single(choice => choice.Name == "Extrude").Run();

        menu.Select(choice => choice.Name).Should().Contain(["Inset", "Cut", "Bevel", "Loop Cut", "Mirror: Off"]);
        tools.Through.Should().BeTrue();
        tools.GetMenu().Select(choice => choice.Name).Should().Contain("Cut Through: On");
        tools.IsDragging.Should().BeTrue();
    }

    [Fact]
    public void AClickSetsAPendingToolAndItsNumbersStillChangeItUntilEnter()
    {
        MeshToolsViewModel tools = EnterBodyWithAFaceSelected();
        int points = ViewModel.CreatorPanel!.FindMesh("body")!.Points.Count;
        int width = MainWindowViewModelTestsBuilder.Width;
        int height = MainWindowViewModelTestsBuilder.Height;

        Viewport.KeyPressed(ViewportKey.I, ViewportButtons.None);
        Viewport.PointerMoved(new Vector2(300, 260), width, height);
        Viewport.PointerMoved(new Vector2(300, 250), width, height);
        Viewport.PointerPressed(new Vector2(300, 250), width, height, additive: false, loop: false);
        bool pending = tools.IsDragging && !tools.IsFollowing;
        Viewport.PointerMoved(new Vector2(300, 100), width, height);
        tools.Thickness = 0.02;
        Viewport.KeyPressed(ViewportKey.X, ViewportButtons.None);
        bool stillPending = tools.IsDragging && tools.ActiveTool == FlickerVoid.Editor.Core.Creators.MeshDragTool.Inset;
        Viewport.KeyPressed(ViewportKey.Enter, ViewportButtons.None);

        pending.Should().BeTrue("a click sets the tool but does not apply it");
        stillPending.Should().BeTrue("only Enter, Apply, Escape or Cancel end it");
        tools.IsDragging.Should().BeFalse();
        ViewModel.CreatorPanel.FindMesh("body")!.Points.Should().HaveCount(points + 4);
    }

    [Fact]
    public void HollowStartsAtTheWallThicknessAndWaitsForApplyWithoutMovingTheMouse()
    {
        Assert.SkipUnless(FlickerVoid.Editor.Core.Meshes.Booleans.SolidBoolean.IsAvailable, "Manifold's library is not built.");
        MeshToolsViewModel tools = EnterBodyWithAFaceSelected();
        int faces = ViewModel.CreatorPanel!.FindMesh("body")!.Faces.Count;
        tools.Thickness = 0.05;

        Viewport.KeyPressed(ViewportKey.H, ViewportButtons.None);
        bool pending = tools.IsDragging && !tools.IsFollowing;
        Viewport.KeyPressed(ViewportKey.Enter, ViewportButtons.None);

        pending.Should().BeTrue("hollow shows at once and its wall is tuned in the Tool tab");
        ViewModel.CreatorPanel.FindMesh("body")!.Faces.Count.Should().BeGreaterThan(faces);
    }

    [Fact]
    public void TheRailOffersTheDoorInEveryCreationEvenWhenItCannotCut()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));

        Viewport.MeshTools.HasDoorTool.Should().BeTrue("the door tool says what it needs rather than disappearing");
    }

    private MeshToolsViewModel EnterBodyWithAFaceSelected()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));
        ViewModel.CreatorPart = "body";
        MeshToolsViewModel tools = Viewport.MeshTools;
        tools.ToggleMesh();
        tools.Element = MeshElement.Face;
        Viewport.Creator!.MeshSelection!.Select(MeshHit.OfFace(2, default), additive: false);
        return tools;
    }

    [Fact]
    public void ALookBuiltForTheCreationShowsInItsPanelAndEnteringAMeshShowsTheGreyboxes()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));
        bool before = VehiclePanel.HasLook;

        _builder.WriteLook(MainWindowViewModelTestsBuilder.Van, "body");
        bool lookShown = VehiclePanel.ShowsLook;
        ViewModel.CreatorPart = "body";
        Viewport.MeshTools.ToggleMesh();

        before.Should().BeFalse();
        VehiclePanel.HasLook.Should().BeTrue();
        lookShown.Should().BeTrue();
        VehiclePanel.ShowsLook.Should().BeFalse("the mesh tools work on the greybox");
        Viewport.KeyPressed(ViewportKey.Escape, ViewportButtons.None);
        Viewport.KeyPressed(ViewportKey.Escape, ViewportButtons.None);
        VehicleLooks looks = _builder.Scene.Game.Services.GetService<VehicleLooks>()!;
        looks.ShowGreyboxes.Should().BeFalse("leaving the creator draws the looks again");
    }

    [Fact]
    public void EnteringClearsTheSelectionSoNoGizmoShows()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));

        Viewport.Selection.Entities.Should().BeEmpty();
    }

    [Fact]
    public void DoubleClickingBesideEverythingEntersNothing()
    {
        _builder.DoubleClickSky();

        Viewport.Creator.Should().BeNull();
        Viewport.Mode.Should().BeSameAs(Viewport.SceneMode);
    }

    [Fact]
    public void EscapeLeavesTheCreator()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));
        World world = _builder.Scene.Game.World;
        Entity crate = _builder.Find("Crate");
        bool greyedInside = world.Has<Greyed>(crate);

        Viewport.KeyPressed(ViewportKey.Escape, ViewportButtons.None);

        greyedInside.Should().BeTrue();
        world.Has<Greyed>(crate).Should().BeFalse("leaving takes the level's grey off");
        Viewport.Creator.Should().BeNull();
        Viewport.Mode.Should().BeSameAs(Viewport.SceneMode);
        Viewport.CreatorFile.Should().BeNull();
        ViewModel.CreatorPanel.Should().BeNull();
        ViewModel.ShowsCreator.Should().BeFalse();
        ViewModel.SideTab.Should().Be(MainWindowViewModel.NoTab, "nothing is selected");
        ViewModel.OutlinerTab.Should().Be(MainWindowViewModel.SceneTab, "the scene comes back");
    }

    [Fact]
    public void ThePartsListAndTheViewportSelectTheSamePart()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));
        List<string?> raised = [];
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.CreatorPart))
            {
                raised.Add(ViewModel.CreatorPart);
            }
        };

        ViewModel.CreatorPart = "wheel.2L";
        string? inViewport = Viewport.Creator!.SelectedPart;
        Viewport.Creator.SelectPart("body");

        inViewport.Should().Be("wheel.2L");
        ViewModel.CreatorPart.Should().Be("body");
        raised.Should().Equal("wheel.2L", "body");
    }

    [Fact]
    public void DoubleClickingOutsideTheCreationLeaves()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));

        _builder.DoubleClickSky();

        Viewport.Creator.Should().BeNull();
    }

    [Fact]
    public void DoubleClickingThePartsAgainStaysInTheCreator()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));
        CreatorMode? creator = Viewport.Creator;

        _builder.DoubleClick(_builder.MiddleOf("Van"));

        Viewport.Creator.Should().BeSameAs(creator);
    }

    [Fact]
    public void UndoRedoAndSaveActOnTheVehicleFile()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));
        VehiclePanel.WheelCount = 6;
        string edited = ViewModel.CreatorHeading;

        ViewModel.Undo();
        int undone = VehiclePanel.WheelCount;
        ViewModel.Redo();
        int redone = VehiclePanel.WheelCount;
        Viewport.Save();

        edited.Should().Be($"{MainWindowViewModelTestsBuilder.Van} *");
        undone.Should().Be(4, "undo walks the file's history and the panel shows the file again");
        redone.Should().Be(6);
        _builder.WheelsOnDisk(MainWindowViewModelTestsBuilder.Van).Should().Be(6, "Ctrl+S saves the creation's file");
        _builder.Scene.IsUnsaved.Should().BeFalse("the creator never edits the level");
        ViewModel.CreatorHeading.Should().Be(MainWindowViewModelTestsBuilder.Van);
    }

    [Fact]
    public void OpeningAVehicleInTheBrowserEditsItAlone()
    {
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Van, AssetTypes.Vehicle);

        CreatorMode? creator = Viewport.Creator;
        creator.Should().NotBeNull();
        creator!.Placement.Should().Be(CreatorPlacement.Alone);
        Viewport.CreatorFile.Should().Be(MainWindowViewModelTestsBuilder.Van);
        ViewModel.DuplicateAndEditCommand.CanExecute(null).Should().BeFalse("only a placed copy carries on in a new file");
    }

    [Fact]
    public void DoubleClickingAGreyboxShapesItInPlaceWithItsMeshEntered()
    {
        _builder.DoubleClick(_builder.MiddleOf("Crate"));

        CreatorMode? creator = Viewport.Creator;
        creator.Should().NotBeNull();
        creator!.Placement.Should().Be(CreatorPlacement.Piece);
        creator.Root.Should().Be(_builder.Find("Crate"));
        creator.MeshSelection.Should().NotBeNull("the mesh tools are ready at once");
        Viewport.MeshTools.IsMeshMode.Should().BeTrue();
        ViewModel.CreatorPanel.Should().BeNull("a greybox has no creator panel");
        ViewModel.CreatorHeading.Should().Be("Crate");
        ViewModel.DuplicateAndEditCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void ShapingAGreyboxIsUndoneAndSavedWithTheLevel()
    {
        _builder.DoubleClick(_builder.MiddleOf("Crate"));
        CreatorMode creator = Viewport.Creator!;
        creator.MeshSelection!.SelectAll();
        creator.MoveMesh(new Vector3(0, 1, 0));
        World world = _builder.Scene.Game.World;
        bool shaped = world.Get<Greybox>(_builder.Find("Crate")).Mesh is not null;

        Viewport.Save();
        string saved = _builder.ReadFile(MainWindowViewModelTestsBuilder.Street);
        ViewModel.Undo();

        shaped.Should().BeTrue();
        saved.Should().Contain("\"mesh\"", "the shape is kept in the level's file");
        world.Get<Greybox>(_builder.Find("Crate")).Mesh.Should().BeNull("undo is the level's");
    }

    [Fact]
    public void EscapeLeavesTheMeshAndThenTheGreybox()
    {
        _builder.DoubleClick(_builder.MiddleOf("Crate"));

        Viewport.KeyPressed(ViewportKey.Escape, ViewportButtons.None);
        bool stillShaping = Viewport.Creator is not null;
        Viewport.KeyPressed(ViewportKey.Escape, ViewportButtons.None);

        stillShaping.Should().BeTrue("the first Escape leaves the mesh");
        Viewport.Creator.Should().BeNull();
    }

    [Fact]
    public void OpeningABuildingInTheBrowserMakesItWithItsPlanToolsAndTree()
    {
        _builder.WriteHouse();

        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.House, AssetTypes.Building);

        Viewport.Creator!.Placement.Should().Be(CreatorPlacement.Alone);
        ViewModel.CreatorKind.Should().Be("Building");
        BuildingPanelViewModel panel = ViewModel.CreatorPanel.Should().BeOfType<BuildingPanelViewModel>().Subject;
        Viewport.Building.Should().NotBeNull();
        Viewport.IsBuildingPlan.Should().BeTrue();
        ViewModel.SideTab.Should().Be(MainWindowViewModel.ToolTab, "the Tool tab comes in front, the Building tab beside it");
        ViewModel.ShowsBuildingParts.Should().BeTrue();
        ViewModel.CreatorNodes.Select(static node => node.Name).Should().StartWith(["House.fvbuilding", "Ground", "Floor", "Outside walls"]);
        Viewport.ToolRail.Groups.SelectMany(static group => group.Items).Select(static item => item.Name).Should().Contain(["Push wall", "Opening", "Camera"]);
        panel.AddStoreyAboveCommand.Execute(null);
        ViewModel.CreatorNodes.Select(static node => node.Name).Should().Contain("Storey 1");
        ViewModel.IsCreatorUnsaved.Should().BeTrue();
        ViewModel.CreatorFileName.Should().Be("House.fvbuilding");
    }

    [Fact]
    public void TheBuildingsKeysTakeItsToolsAndPickingATreeLinePicksInThePlan()
    {
        _builder.WriteHouse();
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.House, AssetTypes.Building);

        Viewport.KeyPressed(ViewportKey.D, ViewportButtons.None);
        FlickerVoid.Editor.Core.Buildings.BuildingTool opening = Viewport.Building!.Editor.Tool;
        Viewport.KeyPressed(ViewportKey.Y, ViewportButtons.None);
        FlickerVoid.Editor.Core.Buildings.BuildingTool camera = Viewport.Building.Editor.Tool;
        ViewModel.CreatorNode = ViewModel.CreatorNodes.Single(static node => node.Name == "Front door");

        opening.Should().Be(FlickerVoid.Editor.Core.Buildings.BuildingTool.Opening);
        camera.Should().Be(FlickerVoid.Editor.Core.Buildings.BuildingTool.Camera);
        Viewport.Building.Editor.Picked!.Kind.Should().Be(FlickerVoid.Editor.Core.Buildings.PlanPickKind.Opening);
        Viewport.Creator!.SelectedPart.Should().Be("storey.0.door.9");
        Viewport.Building.Tool.Title.Should().Be("Camera");
        Viewport.Building.Tool.Subtitle.Should().Be("Front door, Ground");
    }

    [Fact]
    public void TheStoreysAboveTheOneEditedAreHiddenUntilTheirEyeIsOn()
    {
        _builder.WriteHouse();
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.House, AssetTypes.Building);
        BuildingPanelViewModel panel = (BuildingPanelViewModel)ViewModel.CreatorPanel!;
        panel.AddStoreyAboveCommand.Execute(null);
        Viewport.Building!.ShowLevel(0);

        Viewport.Building.Update(0.016f, 800, 600, 1);
        bool[] hidden = [IsHidden("storey.1.floor"), IsHidden("storey.1.walls"), IsHidden("roof"), IsHidden("storey.0.walls")];
        panel.ShowsAbove = true;
        Viewport.Building.Update(0.016f, 800, 600, 1);

        hidden.Should().Equal(true, true, true, false);
        IsHidden("storey.1.walls").Should().BeFalse("the eye shows the storeys above");
    }

    [Fact]
    public void TheRailingsRoundOpeningsAboveAreHiddenWithTheStoreysAbove()
    {
        _builder.WriteHouse(climbs: true);
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.House, AssetTypes.Building);
        Viewport.Building!.ShowLevel(0);

        Viewport.Building.Update(0.016f, 800, 600, 1);

        bool[] hidden = [IsHidden("stairs.1"), IsHidden("stairs.1.railing"), IsHidden("elevator.1.railing")];
        hidden.Should().Equal(false, true, true);
    }

    [Fact]
    public void APushedWallMovesTheBoundsItIsPickedBy()
    {
        _builder.WriteHouse();
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.House, AssetTypes.Building);
        CreationParts parts = _builder.Scene.Game.Services.GetService<CreationParts>()!;
        Entity walls = parts.Find(Viewport.Creator!.Root, "storey.0.walls");
        _builder.Scene.Picker.TryGetBounds(_builder.Scene.Game.World, walls, out Vector3 low, out Vector3 high);
        int east = FlickerVoid.Engine.Buildings.BuildingFile.Create().Storeys[0].Walls[1].Id;

        Viewport.Building!.Editor.Push(0, east, 1);
        Viewport.Building.Editor.Apply();

        walls = parts.Find(Viewport.Creator!.Root, "storey.0.walls");
        _builder.Scene.Picker.TryGetBounds(_builder.Scene.Game.World, walls, out Vector3 newLow, out Vector3 newHigh);
        (newLow, newHigh).Should().NotBe((low, high));
    }

    [Fact]
    public void WalkingNamesTheShotItDraws()
    {
        _builder.WriteHouse();
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.House, AssetTypes.Building);

        Viewport.Building!.ToggleWalk();
        Viewport.Building.Update(0.016f, 800, 600, 1);

        Viewport.Building.LiveShot.Should().Be("Walk Outside");
        Viewport.Building.ToggleWalk();
    }

    [Fact]
    public void LeavingTheBuildingPutsItsPlanToolsAway()
    {
        _builder.WriteHouse();
        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.House, AssetTypes.Building);

        Viewport.LeaveCreator();

        Viewport.Building.Should().BeNull();
        ViewModel.ShowsTool.Should().BeFalse();
    }

    [Fact]
    public void OpeningATreeInTheBrowserMakesItWithItsOwnPanel()
    {
        _builder.WriteShrub(FoliageKind.Tree);

        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Shrub, AssetTypes.Foliage);

        Viewport.Creator!.Placement.Should().Be(CreatorPlacement.Alone);
        ViewModel.CreatorKind.Should().Be("Foliage");
        FoliagePanelViewModel panel = ViewModel.CreatorPanel.Should().BeOfType<FoliagePanelViewModel>().Subject;
        ViewModel.CreatorParts.Should().HaveCount(10);
        panel.Limbs = 1;
        ViewModel.CreatorParts.Should().HaveCount(7);
        ViewModel.CreatorHeading.Should().Be($"{MainWindowViewModelTestsBuilder.Shrub} *");
    }

    [Fact]
    public void DoubleClickingAPlacedPlantMakesItInPlace()
    {
        _builder.WriteShrub(FoliageKind.Bush);
        _builder.PlaceFoliage("Shrub", "foliage", $$"""{ "file": "{{MainWindowViewModelTestsBuilder.Shrub}}" }""");

        _builder.DoubleClick(_builder.MiddleOf("Shrub"));

        Viewport.Creator!.Placement.Should().Be(CreatorPlacement.InPlace);
        ViewModel.CreatorPanel.Should().BeOfType<FoliagePanelViewModel>();
        ViewModel.CreatorParts.Should().Equal("crown.1", "crown.2", "crown.3");
    }

    [Fact]
    public void DoubleClickingScatteredFoliageMakesItsFileAlone()
    {
        _builder.WriteShrub(FoliageKind.Bush);
        _builder.PlaceFoliage("Patch", "foliageScatter", $$"""{ "file": "{{MainWindowViewModelTestsBuilder.Shrub}}", "count": 1, "radius": 0 }""");

        _builder.DoubleClick(_builder.MiddleOf("Patch"));

        Viewport.Creator!.Placement.Should().Be(CreatorPlacement.Alone);
        Viewport.CreatorFile.Should().Be(MainWindowViewModelTestsBuilder.Shrub);
        ViewModel.CreatorParts.Should().Equal("crown.1", "crown.2", "crown.3");
    }

    [Theory]
    [InlineData(FoliageKind.Tree, false)]
    [InlineData(FoliageKind.Bush, true)]
    public void DroppingATreePlacesOnePlantAndABushAScatter(FoliageKind kind, bool scattered)
    {
        _builder.WriteShrub(kind);
        Vector2 corner = new(600, 440);

        Viewport.DropAsset(MainWindowViewModelTestsBuilder.Shrub, corner, MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);

        Entity dropped = _builder.Find("Shrub");
        _builder.Scene.Game.World.Has<FoliageScatter>(dropped).Should().Be(scattered);
        _builder.Scene.Game.World.Has<Plant>(dropped).Should().Be(!scattered);
    }

    [Fact]
    public void DuplicateAndEditCarriesOnInANewFile()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));
        VehiclePanel.WheelCount = 6;
        const string Copy = "Vans/Van 2.fvvehicle";

        ViewModel.DuplicateAndEdit();

        World world = _builder.Scene.Game.World;
        Entity van = _builder.Find("Van");
        world.Get<Vehicle>(van).File.Should().Be(Copy, "the copy entered now names the new file");
        Viewport.Creator!.Root.Should().Be(van);
        Viewport.CreatorFile.Should().Be(Copy);
        VehiclePanel.WheelCount.Should().Be(6, "the edits made so far come along");
        ViewModel.CreatorHeading.Should().Be($"{Copy} *");
        _builder.WheelsOnDisk(MainWindowViewModelTestsBuilder.Van).Should().Be(4);
        ViewModel.UnsavedPaths.Should().NotContain(MainWindowViewModelTestsBuilder.Van, "the first file goes back to how it was saved");
    }

    [Fact]
    public void OpeningAnotherSceneLeavesTheCreatorFirst()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));

        _builder.OpenInBrowser(MainWindowViewModelTestsBuilder.Room);

        Viewport.Creator.Should().BeNull();
        _builder.Scene.ScenePath.Should().Be(MainWindowViewModelTestsBuilder.Room);
    }
}
