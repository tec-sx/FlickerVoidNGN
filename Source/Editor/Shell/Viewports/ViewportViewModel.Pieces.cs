using Arch.Core;

using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Meshes.Booleans;

namespace FlickerVoid.Editor.Viewports;

public sealed partial class ViewportViewModel
{
    /// <summary>Whether the selected entity is a greybox of the level the mesh tools can shape, which shows the Shape button.</summary>
    public bool CanShapeSelected => Creator is null && !IsGameView && Selection.Selected is { } selected && PieceTools.IsPiece(Scene.Game.World, selected);

    /// <summary>Whether several greyboxes are selected, so the others can be cut out of the one selected last.</summary>
    public bool CanCutSelected
    {
        get
        {
            World world = Scene.Game.World;
            return CanShapeSelected && SolidBoolean.IsAvailable && Selection.Entities.Count > 1 && Selection.Entities.All(entity => PieceTools.IsPiece(world, entity));
        }
    }

    /// <summary>Shapes the selected greybox in place with the mesh tools, as a double-click on it does.</summary>
    [RelayCommand]
    public void ShapeSelected()
    {
        if (CanShapeSelected && Selection.Selected is { } selected)
        {
            PieceRequested?.Invoke(selected);
        }
    }

    /// <summary>Cuts the other selected greyboxes out of the one selected last, where they overlap it: a tunnel, a doorway, a window.</summary>
    [RelayCommand]
    public void CutSelected()
    {
        if (!CanCutSelected || Selection.Selected is not { } target)
        {
            return;
        }

        Entity[] cutters = [.. Selection.Entities.Where(entity => entity != target)];
        string name = DescribeMany("Cut", [target]);
        if (PieceTools.Cut(Scene, target, cutters))
        {
            Status = $"{name}. The greyboxes it was cut with stay where they are; delete or hide them to see the opening.";
        }
        else
        {
            Warn("Nothing was cut: the other greyboxes don't reach into the one selected last.");
        }
    }

    private void NotifyPieces()
    {
        OnPropertyChanged(nameof(CanShapeSelected));
        OnPropertyChanged(nameof(CanCutSelected));
    }
}
