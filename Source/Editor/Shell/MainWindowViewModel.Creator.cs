using System.ComponentModel;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Avalonia.Media;

using FlickerVoid.Editor.Assets.Blender;
using FlickerVoid.Editor.Assets.Buildings;
using FlickerVoid.Editor.Assets.Creators;
using FlickerVoid.Editor.Assets.Vehicles;
using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Runtime.Solids;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor;

public sealed partial class MainWindowViewModel
{
    /// <summary>The creator's panel while a vehicle or a building is made, else null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CreatorKind))]
    [NotifyPropertyChangedFor(nameof(CreatorIcon))]
    public partial CreatorPanelViewModel? CreatorPanel { get; private set; }

    /// <summary>What is being made, as the context panel's tab names it: Vehicle, Building.</summary>
    public string CreatorKind => CreatorPanel?.Kind ?? "";

    public Geometry CreatorIcon
    {
        get
        {
            if (CreatorPanel is BuildingPanelViewModel)
            {
                return LucideIcons.Building;
            }

            return LucideIcons.Vehicle;
        }
    }

    partial void OnCreatorPanelChanged(CreatorPanelViewModel? oldValue, CreatorPanelViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= HandleCreatorPanelChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += HandleCreatorPanelChanged;
        }

        OnPropertyChanged(nameof(CreatorParts));
    }

    private void HandleCreatorPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CreatorPanelViewModel.Parts))
        {
            OnPropertyChanged(nameof(CreatorParts));
        }
    }

    /// <summary>The ids of the creation's parts, for the outliner's Parts tab; empty in the scene.</summary>
    public IReadOnlyList<string> CreatorParts => CreatorPanel?.Parts ?? [];

    /// <summary>The part selected in the Parts tab and the viewport alike, or null.</summary>
    public string? CreatorPart
    {
        get => Viewport.Creator?.SelectedPart;
        set => Viewport.Creator?.SelectPart(value);
    }

    /// <summary>The file of the creation being made, marked with an asterisk when it has unsaved edits; empty in the scene.</summary>
    public string CreatorHeading
    {
        get
        {
            if (Viewport.CreatorFile is not { } file)
            {
                return "";
            }

            if (_content.IsUnsaved(file))
            {
                return $"{file} *";
            }

            return file;
        }
    }

    /// <summary>
    /// Carries on making the placed creation in a copy of its file: the copy takes the unsaved edits, the first file goes back to how
    /// it was saved, and the placed entity names the copy.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDuplicateAndEdit))]
    public void DuplicateAndEdit()
    {
        if (Viewport.Creator is not { Placement: CreatorPlacement.InPlace } creator || Viewport.CreatorFile is not { } file)
        {
            return;
        }

        if (Assets.Reveal(file) is null)
        {
            Viewport.Status = $"{file} is not in the content folder.";
            return;
        }

        Assets.Duplicate();
        if (Assets.SelectedAsset is not { } duplicated || duplicated.Path == file)
        {
            Viewport.Status = $"Can't duplicate {file}. {Assets.Status}";
            return;
        }

        string copy = duplicated.Path;
        Entity placed = creator.Root;
        Viewport.LeaveCreator();
        if (_content.IsUnsaved(file))
        {
            byte[] edited = _content.Read(file);
            _content.Edit(copy, edited);
            _content.Discard(file);
        }

        if (IsBuilding(copy))
        {
            Building building = new() { File = copy };
            Viewport.Scene.SetComponent(placed, building);
        }
        else
        {
            Vehicle vehicle = new() { File = copy };
            Viewport.Scene.SetComponent(placed, vehicle);
        }

        EditCreation(placed, copy);
    }

    private bool CanDuplicateAndEdit() => Viewport.Creator?.Placement == CreatorPlacement.InPlace;

    private static bool IsBuilding(string file) => file.EndsWith(BuildingFile.Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Makes the placed creation in place, as a double-click on it asks; a file that can't be read is reported.</summary>
    private void EditCreation(Entity placed, string file)
    {
        if (OpenCreatorPanel(file) is not { } panel)
        {
            return;
        }

        EditorScene scene = Viewport.Scene;
        UndoHistory history = _content.GetHistory(file);
        CreatorMode creator = CreatorMode.InPlace(scene, Viewport.Camera, placed, history, () => SaveCreation(file), FindCreatorMesh, ApplyCreatorMesh, IsCreatorMirrored, SetCreatorMirror, HangCreatorDoor);
        EnterCreator(creator, file, panel);
    }

    /// <summary>Makes the vehicle or building opened in the content browser alone, set apart from the level.</summary>
    private void OpenCreation(string path)
    {
        if (OpenCreatorPanel(path) is not { } panel)
        {
            return;
        }

        EditorScene scene = Viewport.Scene;
        UndoHistory history = _content.GetHistory(path);
        CreatorMode creator;
        if (IsBuilding(path))
        {
            Building building = new() { File = path };
            creator = CreatorMode.Alone(scene, Viewport.Camera, building, history, () => SaveCreation(path), FindCreatorMesh, ApplyCreatorMesh, IsCreatorMirrored, SetCreatorMirror, HangCreatorDoor);
        }
        else
        {
            Vehicle vehicle = new() { File = path };
            creator = CreatorMode.Alone(scene, Viewport.Camera, vehicle, history, () => SaveCreation(path), FindCreatorMesh, ApplyCreatorMesh, IsCreatorMirrored, SetCreatorMirror, HangCreatorDoor);
        }

        EnterCreator(creator, path, panel);
    }

    private void EnterCreator(CreatorMode creator, string file, CreatorPanelViewModel panel)
    {
        Viewport.EnterCreator(creator, file);
        creator.PartSelected += () => OnPropertyChanged(nameof(CreatorPart));
        creator.MeshModeChanged += ShowGreyboxesInTheMesh;
        CreatorPanel = panel;
        ShowContext(CreatorTab);
        OutlinerTab = PartsTab;
        Viewport.Status = $"Making {file}. Escape, or a double-click beside it, goes back to the level.";
    }

    private CreatorPanelViewModel? OpenCreatorPanel(string path)
    {
        try
        {
            if (IsBuilding(path))
            {
                BuildingDocument building = new(_content, path);
                return new BuildingPanelViewModel(building);
            }

            VehicleDocument document = new(_content, path);
            return new VehiclePanelViewModel(document, CreateLookTools());
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Viewport.Status = $"Can't open {path}: {exception.Message}";
            return null;
        }
    }

    /// <summary>The look tools over the scene's vehicle looks; none when the game has no vehicles.</summary>
    private VehicleLookTools? CreateLookTools()
    {
        if (Viewport.Scene.Game.Services.GetService<VehicleLooks>() is not { } looks)
        {
            return null;
        }

        BlenderLauncher launcher = new();
        BlenderRoundTrip blender = new(_content, launcher, BlenderLocator.Find);
        return new VehicleLookTools(_content, blender, looks);
    }

    /// <summary>Reads the creation's look again when the content build baked it anew.</summary>
    private void ShowCreationLook(string fullPath)
    {
        if (CreatorPanel is not VehiclePanelViewModel panel || Viewport.CreatorFile is not { } file)
        {
            return;
        }

        string look = VehicleLook.GetBuiltPath(file);
        if (IsPath(look, fullPath))
        {
            panel.RefreshLook();
        }
    }

    /// <summary>The mesh tools work on the greybox, so entering a part's mesh shows the greyboxes under the look.</summary>
    private void ShowGreyboxesInTheMesh()
    {
        if (Viewport.Creator?.MeshSelection is not null && CreatorPanel is VehiclePanelViewModel { ShowsLook: true } panel)
        {
            panel.ShowsLook = false;
        }
    }

    private SolidMesh? FindCreatorMesh(string id) => CreatorPanel?.FindMesh(id);

    private string? HangCreatorDoor(string partId, int face, SolidEdge hinge, DoorOptions options) => CreatorPanel?.HangDoor(partId, face, hinge, options);

    private bool IsCreatorMirrored(string id) => CreatorPanel?.FindMirror(id) == true;

    private void SetCreatorMirror(string id, bool mirror) => CreatorPanel?.SetMirror(id, mirror);

    private void ApplyCreatorMesh(string id, SolidMesh mesh) => CreatorPanel?.SetMesh(id, mesh);

    private bool SaveCreation(string path)
    {
        _content.Save(path);
        return true;
    }

    /// <summary>Shows the creation's file again when an undo, a discard or a change on disk put other contents in it.</summary>
    private void ShowCreation(string fullPath)
    {
        if (CreatorPanel is null || Viewport.CreatorFile is not { } file || !IsPath(file, fullPath))
        {
            return;
        }

        if (OpenCreatorPanel(file) is { } panel)
        {
            CreatorPanel = panel;
        }
    }

    /// <summary>Hides the creator's panel when the viewport leaves the creator, and follows the history its undo works on.</summary>
    private void HandleViewportChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Viewport.Mode))
        {
            return;
        }

        if (Viewport.Creator is null)
        {
            if (CreatorPanel is VehiclePanelViewModel left)
            {
                left.ShowsLook = true;
            }

            CreatorPanel = null;
            HideContext(CreatorTab);
            if (OutlinerTab == PartsTab)
            {
                OutlinerTab = SceneTab;
            }
        }

        OnPropertyChanged(nameof(CreatorHeading));
        OnPropertyChanged(nameof(CreatorPart));
        DuplicateAndEditCommand.NotifyCanExecuteChanged();
        WatchHistory();
    }
}
