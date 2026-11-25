using Arch.Core;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Creators;
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
        ViewModel.SideTab.Should().Be(MainWindowViewModel.CreatorTab, "the creator's tools take the side panel");
        ViewModel.CreatorHeading.Should().Be(MainWindowViewModelTestsBuilder.Van);
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
        ViewModel.SideTab.Should().Be(0, "the inspector comes back");
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
