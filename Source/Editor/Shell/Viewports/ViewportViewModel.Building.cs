using System.Numerics;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Buildings;
using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Viewports;

public sealed partial class ViewportViewModel
{
    /// <summary>The building creator's plan tools while a building is made, else null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBuildingPlan))]
    public partial BuildingPlanViewModel? Building { get; set; }

    /// <summary>Whether a building's plan tools are in hand: a building is made and no mesh is entered.</summary>
    public bool IsBuildingPlan => Building is not null && !MeshTools.IsMeshMode;

    partial void OnBuildingChanged(BuildingPlanViewModel? oldValue, BuildingPlanViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Editor.Changed -= ToolRail.Refresh;
            oldValue.PropertyChanged -= HandleBuildingChanged;
        }

        if (newValue is not null)
        {
            newValue.Editor.Changed += ToolRail.Refresh;
            newValue.PropertyChanged += HandleBuildingChanged;
        }

        ToolRail.Refresh();
    }

    private void HandleBuildingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BuildingPlanViewModel.IsWalking))
        {
            ToolRail.Refresh();
        }
    }

    /// <summary>Runs the building creator's frame: the walk, the storeys shown and the labels.</summary>
    public void UpdateBuilding(float seconds, int width, int height, double scaling) => Building?.Update(seconds, width, height, scaling);

    /// <summary>The world the viewport draws: the one Walk plays while walking, else the scene's.</summary>
    public World GetDrawnWorld() => Building?.WalkWorld ?? Scene.Game.World;

    /// <summary>The camera the viewport draws with: the live shot while walking, else the editor's.</summary>
    public RenderCamera GetDrawnCamera()
    {
        if (Building?.WalkView is { } walking)
        {
            return walking;
        }

        Camera camera = Camera.ToCamera();
        Transform transform = Camera.ToTransform();
        return new RenderCamera(camera, transform);
    }

    /// <summary>A click while walking walks; the building's plan tools see the rest through the creator.</summary>
    private bool PressBuilding(Vector2 pixel, int width, int height)
    {
        if (Building is not { IsWalking: true } building)
        {
            return false;
        }

        building.Click(pixel, width, height);
        return true;
    }

    /// <summary>
    /// The building creator's keys, while its plan is in hand: D, S, T, O and Y take the opening, stairs, elevator, roof and camera
    /// tools, Q selects, P looks down on the plan, Enter applies the pending edit and Escape cancels it, drops the pick, then stops a
    /// walk.
    /// </summary>
    private bool PressBuildingKey(ViewportKey key)
    {
        if (Building is not { } building)
        {
            return false;
        }

        if (building.IsWalking)
        {
            if (key == ViewportKey.Escape)
            {
                building.ToggleWalk();
            }

            return true;
        }

        if (MeshTools.IsMeshMode)
        {
            return false;
        }

        BuildingEditor editor = building.Editor;
        switch (key)
        {
            case ViewportKey.Enter when editor.IsPending:
                editor.Apply();
                return true;
            case ViewportKey.Escape when editor.IsPending:
                editor.Cancel();
                return true;
            case ViewportKey.Escape when editor.Picked is not null || editor.Tool != BuildingTool.Select:
                editor.Pick(null);
                building.Choose(BuildingTool.Select);
                return true;
            case ViewportKey.Q:
                building.Choose(BuildingTool.Select);
                return true;
            case ViewportKey.D:
                building.Choose(BuildingTool.Opening);
                return true;
            case ViewportKey.S:
                building.Choose(BuildingTool.Stairs);
                return true;
            case ViewportKey.T:
                building.Choose(BuildingTool.Elevator);
                return true;
            case ViewportKey.O:
                building.Choose(BuildingTool.Roof);
                return true;
            case ViewportKey.Y:
                building.Choose(BuildingTool.Camera);
                return true;
            case ViewportKey.P:
                building.ToggleTopView();
                return true;
            default:
                return false;
        }
    }
}
