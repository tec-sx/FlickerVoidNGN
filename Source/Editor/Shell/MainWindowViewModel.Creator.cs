using System.ComponentModel;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets.Vehicles;
using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Runtime.Solids;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor;

public sealed partial class MainWindowViewModel
{
    /// <summary>The vehicle creator's panel while a vehicle is made, else null.</summary>
    [ObservableProperty]
    public partial VehiclePanelViewModel? CreatorPanel { get; private set; }

    partial void OnCreatorPanelChanged(VehiclePanelViewModel? oldValue, VehiclePanelViewModel? newValue)
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
        if (e.PropertyName == nameof(VehiclePanelViewModel.Parts))
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

        Vehicle vehicle = new() { File = copy };
        Viewport.Scene.SetComponent(placed, vehicle);
        EditCreation(placed, copy);
    }

    private bool CanDuplicateAndEdit() => Viewport.Creator?.Placement == CreatorPlacement.InPlace;

    /// <summary>Makes the placed vehicle in place, as a double-click on it asks; a file that can't be read is reported.</summary>
    private void EditCreation(Entity placed, string file)
    {
        if (OpenVehiclePanel(file) is not { } panel)
        {
            return;
        }

        EditorScene scene = Viewport.Scene;
        UndoHistory history = _content.GetHistory(file);
        CreatorMode creator = CreatorMode.InPlace(scene, Viewport.Camera, placed, history, () => SaveCreation(file), FindCreatorMesh, ApplyCreatorMesh);
        EnterCreator(creator, file, panel);
    }

    /// <summary>Makes the vehicle opened in the content browser alone, set apart from the level.</summary>
    private void OpenVehicle(string path)
    {
        if (OpenVehiclePanel(path) is not { } panel)
        {
            return;
        }

        EditorScene scene = Viewport.Scene;
        UndoHistory history = _content.GetHistory(path);
        Vehicle vehicle = new() { File = path };
        CreatorMode creator = CreatorMode.Alone(scene, Viewport.Camera, vehicle, history, () => SaveCreation(path), FindCreatorMesh, ApplyCreatorMesh);
        EnterCreator(creator, path, panel);
    }

    private void EnterCreator(CreatorMode creator, string file, VehiclePanelViewModel panel)
    {
        Viewport.EnterCreator(creator, file);
        creator.PartSelected += () => OnPropertyChanged(nameof(CreatorPart));
        CreatorPanel = panel;
        ShowContext(CreatorTab);
        OutlinerTab = PartsTab;
        Viewport.Status = $"Making {file}. Escape, or a double-click beside it, goes back to the level.";
    }

    private VehiclePanelViewModel? OpenVehiclePanel(string path)
    {
        VehicleDocument document;
        try
        {
            document = new VehicleDocument(_content, path);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Viewport.Status = $"Can't open {path}: {exception.Message}";
            return null;
        }

        return new VehiclePanelViewModel(document);
    }

    private SolidMesh? FindCreatorMesh(string id) => CreatorPanel?.FindMesh(id);

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

        if (OpenVehiclePanel(file) is { } panel)
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
