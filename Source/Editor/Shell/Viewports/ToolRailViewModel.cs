using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Gizmos;

namespace FlickerVoid.Editor.Viewports;

/// <summary>
/// The vertical tool rail on the viewport's left edge, one component for every view: it lists the tools of what is being edited in
/// groups, lit while chosen or while their key is held. The level and the creators fill it with their own; the select, move, rotate and
/// scale tools come first everywhere, the mesh tools follow in the mesh.
/// </summary>
public sealed partial class ToolRailViewModel : ObservableObject
{
    private readonly ViewportViewModel _viewport;

    public ToolRailViewModel(ViewportViewModel viewport)
    {
        _viewport = viewport;
        viewport.PropertyChanged += HandleViewportChanged;
        viewport.MeshTools.PropertyChanged += HandleToolsChanged;
        Groups = Build();
    }

    /// <summary>The tools in groups, a hairline between each two.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ToolGroup> Groups { get; private set; }

    public void Refresh()
    {
        Groups = Build();
    }

    private IReadOnlyList<ToolGroup> Build()
    {
        MeshToolsViewModel tools = _viewport.MeshTools;
        GizmoMode mode = _viewport.GizmoMode;
        List<ToolGroup> groups =
        [
            new ToolGroup(
            [
                new ToolItem("Select", "Q", nameof(LucideIcons.Select), () => _viewport.GizmoMode = GizmoMode.Select, mode == GizmoMode.Select),
                new ToolItem("Move", "W", nameof(LucideIcons.Move), () => _viewport.GizmoMode = GizmoMode.Move, mode == GizmoMode.Move),
                new ToolItem("Rotate", "E", nameof(LucideIcons.Rotate), () => _viewport.GizmoMode = GizmoMode.Rotate, mode == GizmoMode.Rotate),
                new ToolItem("Scale", "R", nameof(LucideIcons.Scale), () => _viewport.GizmoMode = GizmoMode.Scale, mode == GizmoMode.Scale),
            ], false),
        ];
        if (tools.IsMeshMode)
        {
            MeshDragTool? active = tools.ActiveTool;
            groups.Add(new ToolGroup(
            [
                new ToolItem("Extrude", "X", nameof(LucideIcons.Extrude), tools.DragExtrude, active == MeshDragTool.Extrude),
                new ToolItem("Inset", "I", nameof(LucideIcons.Inset), tools.DragInset, active == MeshDragTool.Inset),
                new ToolItem("Bevel", "B", nameof(LucideIcons.Bevel), tools.DragBevel, active == MeshDragTool.Bevel),
                new ToolItem("Loop cut", "L", nameof(LucideIcons.LoopCut), tools.LoopCut, false),
                new ToolItem("Cut", "C", nameof(LucideIcons.Cut), tools.DragCut, active == MeshDragTool.Cut),
                new ToolItem("Hollow", "H", nameof(LucideIcons.Hollow), tools.DragHollow, active == MeshDragTool.Hollow),
            ], true));
            groups.Add(new ToolGroup(
            [
                new ToolItem("Mirror", "M", nameof(LucideIcons.Mirror), () => tools.Mirror = !tools.Mirror, tools.Mirror),
            ], true));
        }

        if (tools.CanDoor)
        {
            groups.Add(new ToolGroup([new ToolItem("Door", "D", nameof(LucideIcons.Door), tools.ToggleDoor, tools.IsDoorTool)], true));
        }

        return groups;
    }

    private void HandleViewportChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewportViewModel.GizmoMode) or nameof(ViewportViewModel.Mode) or nameof(ViewportViewModel.IsCreating))
        {
            Refresh();
        }
    }

    private void HandleToolsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MeshToolsViewModel.IsMeshMode) or nameof(MeshToolsViewModel.ActiveTool) or nameof(MeshToolsViewModel.Mirror) or nameof(MeshToolsViewModel.IsDoorTool) or nameof(MeshToolsViewModel.CanDoor))
        {
            Refresh();
        }
    }
}
