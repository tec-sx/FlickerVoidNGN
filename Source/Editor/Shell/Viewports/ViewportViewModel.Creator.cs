using System.ComponentModel;
using System.Numerics;

using Arch.Core;

using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Greyboxes;
using FlickerVoid.Engine.Buildings;
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

    /// <summary>Raised when a greybox of the level is double-clicked, with its entity, to shape it in place.</summary>
    public event Action<Entity>? PieceRequested;

    /// <summary>
    /// A double-click: on a placed creation it asks to make it in place, and on a greybox of the level to shape it; in the creator, one that misses the creation leaves it. The
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
        else if (world.TryGet(picked, out Building building) && !string.IsNullOrEmpty(building.File))
        {
            CreationRequested?.Invoke(picked, building.File);
        }
        else if (world.TryGet(picked, out Greybox greybox) && greybox.GetShownSwap() is null)
        {
            PieceRequested?.Invoke(picked);
        }
    }

    /// <summary>Makes the creation in the viewport: clicks go to it, the selection is cleared and the view frames it.</summary>
    public void EnterCreator(CreatorMode creator, string file)
    {
        LeaveCreator();
        SceneMode.PointerReleased();
        Selection.Select(null);
        Mode = creator;
        creator.MeshModeChanged += MeshTools.Refresh;
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
        creator.MeshModeChanged -= MeshTools.Refresh;
        CreatorFile = null;
        creator.Dispose();
        NotifyCreator();
    }

    /// <summary>
    /// The mesh keys of the creator: Tab enters and leaves the mesh, then 1, 2 and 3 pick points, edges or faces, E extrudes, I
    /// insets, C cuts and Ctrl+B bevels by an amount the mouse then sets (any key keeps it, Escape cancels it), Ctrl+R cuts a loop and D hangs a door where the mouse is. Escape leaves the mesh before the
    /// creator. True when the key was taken.
    /// </summary>
    private bool PressMeshKey(ViewportKey key, bool ctrl)
    {
        if (MeshTools.IsDragging)
        {
            if (key == ViewportKey.Escape)
            {
                return MeshTools.CancelDrag();
            }

            MeshTools.ConfirmDrag();
        }

        if (key == ViewportKey.Tab)
        {
            MeshTools.ToggleMesh();
            return true;
        }

        if (!MeshTools.IsMeshMode)
        {
            return false;
        }

        switch (key)
        {
            case ViewportKey.One:
                MeshTools.Element = MeshElement.Point;
                break;
            case ViewportKey.Two:
                MeshTools.Element = MeshElement.Edge;
                break;
            case ViewportKey.Three:
                MeshTools.Element = MeshElement.Face;
                break;
            case ViewportKey.E:
                MeshTools.DragExtrude();
                break;
            case ViewportKey.I:
                MeshTools.DragInset();
                break;
            case ViewportKey.C:
                MeshTools.DragCut();
                break;
            case ViewportKey.B when ctrl:
                MeshTools.DragBevel();
                break;
            case ViewportKey.R when ctrl:
                MeshTools.LoopCut();
                break;
            case ViewportKey.D:
                MeshTools.HangDoor();
                break;
            case ViewportKey.Escape:
                MeshTools.Leave();
                break;
            default:
                return false;
        }

        return true;
    }

    private void NotifyCreator()
    {
        OnPropertyChanged(CreatorChangedArgs);
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(IsCreating));
        OnPropertyChanged(nameof(CreatorFile));
        NotifyPieces();
        MeshTools.Refresh();
    }
}
