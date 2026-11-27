using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Buildings;
using FlickerVoid.Editor.Core.Buildings;
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

        if (_viewport.Building is { IsWalking: false } building && !tools.IsMeshMode)
        {
            AddBuildingTools(groups, building);
            return groups;
        }

        if (tools.HasDoorTool)
        {
            groups.Add(new ToolGroup([new ToolItem("Door", "D", nameof(LucideIcons.Door), tools.ToggleDoor, tools.IsDoorTool)], true));
        }

        return groups;
    }

    /// <summary>
    /// The building creator's plan tools in the mockup's order: select first (Q, beside the gizmo's move, rotate and scale), then the plan's shape
    /// (push wall, move corner, split, arc, wall, room, pillar), what crosses it (opening D, stairs S, elevator T, roof O) and the
    /// camera (Y).
    /// </summary>
    private static void AddBuildingTools(List<ToolGroup> groups, BuildingPlanViewModel building)
    {
        BuildingTool tool = building.Editor.Tool;
        List<ToolItem> first = [.. groups[0].Items];
        first[0] = Item(building, "Select", "Q", nameof(LucideIcons.Select), BuildingTool.Select, tool);
        groups[0] = new ToolGroup(first, false);
        groups.Add(new ToolGroup(
        [
            Item(building, "Push wall", null, nameof(LucideIcons.PushWall), BuildingTool.PushWall, tool),
            Item(building, "Move corner", null, nameof(LucideIcons.MoveCorner), BuildingTool.MoveCorner, tool),
            Item(building, "Split wall", null, nameof(LucideIcons.SplitWall), BuildingTool.SplitWall, tool),
            Item(building, "Arc", null, nameof(LucideIcons.Arc), BuildingTool.Arc, tool),
            Item(building, "Wall", null, nameof(LucideIcons.Wall), BuildingTool.Wall, tool),
            Item(building, "Room", null, nameof(LucideIcons.Room), BuildingTool.Room, tool),
            Item(building, "Pillar", null, nameof(LucideIcons.Pillar), BuildingTool.Pillar, tool),
        ], true));
        groups.Add(new ToolGroup(
        [
            Item(building, "Opening", "D", nameof(LucideIcons.Door), BuildingTool.Opening, tool),
            Item(building, "Stairs", "S", nameof(LucideIcons.Steps), BuildingTool.Stairs, tool),
            Item(building, "Elevator", "T", nameof(LucideIcons.Elevator), BuildingTool.Elevator, tool),
            Item(building, "Roof", "O", nameof(LucideIcons.Roof), BuildingTool.Roof, tool),
        ], true));
        groups.Add(new ToolGroup([Item(building, "Camera", "Y", nameof(LucideIcons.Camera), BuildingTool.Camera, tool)], true));
    }

    private static ToolItem Item(BuildingPlanViewModel building, string name, string? key, string icon, BuildingTool tool, BuildingTool chosen)
    {
        return new ToolItem(name, key, icon, () => building.Choose(tool), tool == chosen);
    }

    private void HandleViewportChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewportViewModel.GizmoMode) or nameof(ViewportViewModel.Mode) or nameof(ViewportViewModel.IsCreating) or nameof(ViewportViewModel.Building))
        {
            Refresh();
        }
    }

    private void HandleToolsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MeshToolsViewModel.IsMeshMode) or nameof(MeshToolsViewModel.ActiveTool) or nameof(MeshToolsViewModel.Mirror) or nameof(MeshToolsViewModel.IsDoorTool) or nameof(MeshToolsViewModel.HasDoorTool))
        {
            Refresh();
        }
    }
}
