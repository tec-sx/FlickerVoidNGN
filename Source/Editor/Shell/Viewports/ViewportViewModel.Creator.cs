using System.ComponentModel;
using System.Numerics;

using Arch.Core;

using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Viewports;

public sealed partial class ViewportViewModel
{
    private static readonly PropertyChangedEventArgs CreatorChangedArgs = new(nameof(Creator));

    /// <summary>The creation being made, while the viewport is in its creator; null in the scene.</summary>
    public CreatorMode? Creator => Mode as CreatorMode;

    /// <summary>Whether a creation is being made, which hides the scene's icons, selection and gizmo.</summary>
    public bool IsCreating => Creator is not null;

    /// <summary>The file of the creation being made, as a content path, or null in the scene.</summary>
    public string? CreatorFile { get; private set; }

    /// <summary>Raised when a placed creation is double-clicked, with its entity and file, to make it in place.</summary>
    public event Action<Entity, string>? CreationRequested;

    /// <summary>
    /// A double-click: on a placed creation it asks to make it in place; in the creator, one that misses the creation leaves it. The
    /// game view only selects.
    /// </summary>
    public void DoubleClicked(Vector2 pixel, int width, int height)
    {
        World world = Scene.Game.World;
        Ray ray = Camera.GetRay(pixel, width, height);
        if (Creator is { } creator)
        {
            if (Scene.Picker.PickPart(world, ray, creator.Root) is null)
            {
                LeaveCreator();
            }

            return;
        }

        if (IsGameView || Scene.Picker.Pick(world, ray) is not { } picked)
        {
            return;
        }

        if (world.TryGet(picked, out Vehicle vehicle) && !string.IsNullOrEmpty(vehicle.File))
        {
            CreationRequested?.Invoke(picked, vehicle.File);
        }
    }

    /// <summary>Makes the creation in the viewport: clicks go to it, the selection is cleared and the view frames it.</summary>
    public void EnterCreator(CreatorMode creator, string file)
    {
        LeaveCreator();
        SceneMode.PointerReleased();
        Selection.Select(null);
        Mode = creator;
        CreatorFile = file;
        NotifyCreator();
        FrameSelected();
    }

    /// <summary>Leaves the creator, if one is open, for the scene: its marks, light and temporary copy go.</summary>
    [RelayCommand]
    public void LeaveCreator()
    {
        if (Creator is not { } creator)
        {
            return;
        }

        Mode = SceneMode;
        CreatorFile = null;
        creator.Dispose();
        NotifyCreator();
    }

    private void NotifyCreator()
    {
        OnPropertyChanged(CreatorChangedArgs);
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(IsCreating));
        OnPropertyChanged(nameof(CreatorFile));
    }
}
