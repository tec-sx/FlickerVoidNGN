using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Editor.Viewports;
using FlickerVoid.Engine.Rendering.Extraction;
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

    public void Dispose() => _builder.Dispose();

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
        ViewModel.CreatorPanel!.Type.Should().Be(VehicleType.Car);
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
    public void ExtrudeFollowsTheMouseUntilAClickKeepsIt()
    {
        MeshToolsViewModel tools = EnterBodyWithAFaceSelected();
        int points = ViewModel.CreatorPanel!.FindMesh("body")!.Points.Count;
        Viewport.PointerMoved(new Vector2(300, 300), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);

        Viewport.KeyPressed(ViewportKey.E, ViewportButtons.None);
        Viewport.PointerMoved(new Vector2(300, 260), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);
        Viewport.PointerMoved(new Vector2(300, 200), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height);
        bool dragging = tools.IsDragging;
        Viewport.PointerPressed(new Vector2(300, 200), MainWindowViewModelTestsBuilder.Width, MainWindowViewModelTestsBuilder.Height, additive: false, loop: false);

        dragging.Should().BeTrue();
        tools.IsDragging.Should().BeFalse();
        tools.Amount.Should().BeGreaterThan(0);
        ViewModel.CreatorPanel.FindMesh("body")!.Points.Should().HaveCount(points + 4);
        Viewport.Creator!.MeshSelection!.Faces.Should().ContainSingle("the click kept the tool's work and selected nothing else");
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
        Viewport.KeyPressed(ViewportKey.E, ViewportButtons.None);

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

        menu.Select(choice => choice.Name).Should().Contain(["Inset", "Cut", "Bevel", "Loop Cut", "Hang Door", "Test Door", "Mirror: Off"]);
        tools.Through.Should().BeTrue();
        tools.GetMenu().Select(choice => choice.Name).Should().Contain("Cut Through: On");
        tools.IsDragging.Should().BeTrue();
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
    public void EnteringClearsTheSelectionSoNoGizmoShows()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));

        Viewport.Selection.Entities.Should().BeEmpty();
    }

    [Fact]
    public void DoubleClickingWhatIsNoCreationEntersNothing()
    {
        _builder.DoubleClick(_builder.MiddleOf("Crate"));

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
        ViewModel.CreatorPanel!.WheelCount = 6;
        string edited = ViewModel.CreatorHeading;

        ViewModel.Undo();
        int undone = ViewModel.CreatorPanel!.WheelCount;
        ViewModel.Redo();
        int redone = ViewModel.CreatorPanel!.WheelCount;
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
    public void DuplicateAndEditCarriesOnInANewFile()
    {
        _builder.DoubleClick(_builder.MiddleOf("Van"));
        ViewModel.CreatorPanel!.WheelCount = 6;
        const string Copy = "Vans/Van 2.fvvehicle";

        ViewModel.DuplicateAndEdit();

        World world = _builder.Scene.Game.World;
        Entity van = _builder.Find("Van");
        world.Get<Vehicle>(van).File.Should().Be(Copy, "the copy entered now names the new file");
        Viewport.Creator!.Root.Should().Be(van);
        Viewport.CreatorFile.Should().Be(Copy);
        ViewModel.CreatorPanel!.WheelCount.Should().Be(6, "the edits made so far come along");
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
