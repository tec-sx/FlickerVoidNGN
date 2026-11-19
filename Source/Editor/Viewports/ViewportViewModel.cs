using System.Numerics;

using Arch.Core;

using Avalonia.Input;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Editor.Cameras;
using FlickerVoid.Engine.Editor.Geometry;
using FlickerVoid.Engine.Editor.Gizmos;
using FlickerVoid.Engine.Editor.Overlays;
using FlickerVoid.Engine.Editor.Scenes;
using FlickerVoid.Engine.Editor.Selection;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Editor.Viewports;

/// <summary>
/// The scene view: the world it shows, the editor camera looking at it, the icons of lights, cameras and volumes, the selection and
/// its gizmo. Clicking selects what is under the mouse, icons first, dragging a gizmo handle changes the selection's transform, and
/// Q, W, E and R switch the gizmo as in Unreal.
/// </summary>
public sealed partial class ViewportViewModel : ObservableObject
{
    private const float SelectionLineWidth = 2;
    private const float ReachLineWidth = 1.5f;
    private const int ConeEdges = 4;

    private static readonly Vector4 SelectionColor = EditorIcons.SelectedColor;

    private readonly EditorCameraController _controller;

    public ViewportViewModel(EditorScene scene, bool validation)
    {
        Scene = scene;
        Validation = validation;
        Camera.LookAt(Vector3.Zero, MathF.PI / 4, -0.5f, 12);
        _controller = new EditorCameraController(Camera);
        Selection.Changed += HandleSelectionChanged;
    }

    public EditorScene Scene { get; }

    public EditorCamera Camera { get; } = new();

    public EditorSelection Selection { get; } = new();

    public TransformGizmo Gizmo { get; } = new();

    public EditorIcons Icons { get; } = new();

    /// <summary>Whether the viewport's renderer turns on the Vulkan validation layer.</summary>
    public bool Validation { get; }

    /// <summary>What the viewport shows on, or why it can't.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = "Starting the viewport.";

    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

    /// <summary>The selected entity's name, or what to do to select one.</summary>
    public string SelectionText
    {
        get
        {
            if (Selection.Selected is not { } entity)
            {
                return "Nothing selected";
            }

            World world = Scene.Game.World;
            if (world.TryGet(entity, out EntityName name))
            {
                return name.Value;
            }

            return $"Entity {entity.Id}";
        }
    }

    public bool IsPerspective
    {
        get => Camera.Projection == CameraProjection.Perspective;
        set
        {
            CameraProjection projection = CameraProjection.Orthographic;
            if (value)
            {
                projection = CameraProjection.Perspective;
            }

            if (projection == Camera.Projection)
            {
                return;
            }

            Camera.Projection = projection;
            OnPropertyChanged();
        }
    }

    public GizmoMode GizmoMode
    {
        get => Gizmo.Mode;
        set
        {
            if (value == Gizmo.Mode)
            {
                return;
            }

            Gizmo.Mode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSelectMode));
            OnPropertyChanged(nameof(IsMoveMode));
            OnPropertyChanged(nameof(IsRotateMode));
            OnPropertyChanged(nameof(IsScaleMode));
        }
    }

    public bool IsSelectMode
    {
        get => GizmoMode == GizmoMode.Select;
        set => SetModeIf(value, GizmoMode.Select);
    }

    public bool IsMoveMode
    {
        get => GizmoMode == GizmoMode.Move;
        set => SetModeIf(value, GizmoMode.Move);
    }

    public bool IsRotateMode
    {
        get => GizmoMode == GizmoMode.Rotate;
        set => SetModeIf(value, GizmoMode.Rotate);
    }

    public bool IsScaleMode
    {
        get => GizmoMode == GizmoMode.Scale;
        set => SetModeIf(value, GizmoMode.Scale);
    }

    /// <summary>Moves the camera by one frame of input over a viewport the given number of pixels high.</summary>
    public void Update(in ViewportInput input, float seconds, int viewportHeight)
    {
        _controller.Apply(input, seconds, viewportHeight);
    }

    /// <summary>Q, W, E and R switch the gizmo, unless the right button is held, when they fly the camera.</summary>
    public void KeyPressed(Key key, ViewportButtons held)
    {
        if ((held & ViewportButtons.Right) != 0)
        {
            return;
        }

        switch (key)
        {
            case Key.Q:
                GizmoMode = GizmoMode.Select;
                break;
            case Key.W:
                GizmoMode = GizmoMode.Move;
                break;
            case Key.E:
                GizmoMode = GizmoMode.Rotate;
                break;
            case Key.R:
                GizmoMode = GizmoMode.Scale;
                break;
        }
    }

    /// <summary>A left click without Alt: starts dragging the gizmo handle under the mouse, or selects what is there.</summary>
    public void PointerPressed(Vector2 pixel, int width, int height)
    {
        World world = Scene.Game.World;
        if (Selection.Selected is { } selected && world.IsAlive(selected))
        {
            Transform transform = world.Get<Transform>(selected);
            if (Gizmo.BeginDrag(Camera, transform, pixel, width, height))
            {
                return;
            }
        }

        Icons.Collect(world, Selection.Selected);
        Entity? picked = Icons.HitTest(Camera, pixel, width, height);
        if (picked is null)
        {
            Ray ray = Camera.GetRay(pixel, width, height);
            picked = Scene.Picker.Pick(world, ray);
        }

        Selection.Select(picked);
    }

    /// <summary>Drags the gizmo, or finds the handle under the mouse.</summary>
    public void PointerMoved(Vector2 pixel, int width, int height)
    {
        World world = Scene.Game.World;
        if (Selection.Selected is not { } selected || !world.IsAlive(selected))
        {
            return;
        }

        ref Transform transform = ref world.Get<Transform>(selected);
        if (Gizmo.IsDragging)
        {
            transform = Gizmo.Drag(Camera, pixel, width, height);
            return;
        }

        Gizmo.Hover(Camera, transform, pixel, width, height);
    }

    public void PointerReleased()
    {
        Gizmo.EndDrag();
    }

    /// <summary>Fills the overlay with this frame's icons, the selection's outline or reach, and the gizmo.</summary>
    public void BuildOverlay(OverlayBuilder overlay, int viewportHeight)
    {
        overlay.Begin(Camera, viewportHeight);
        World world = Scene.Game.World;
        Icons.Collect(world, Selection.Selected);
        foreach (IconInstance icon in Icons.Icons)
        {
            overlay.AddIcon(icon.Position, icon.Icon, icon.Color);
        }

        if (Selection.Selected is not { } selected || !world.IsAlive(selected))
        {
            return;
        }

        Transform transform = world.Get<Transform>(selected);
        DrawSelection(overlay, world, selected, transform);
        Gizmo.Draw(overlay, Camera, transform, viewportHeight);
    }

    /// <summary>A mesh's box, a point light's reach as three circles, a spot light's cone, or an irradiance volume's box.</summary>
    private void DrawSelection(OverlayBuilder overlay, World world, Entity selected, in Transform transform)
    {
        if (Scene.Picker.TryGetBounds(world, selected, out Vector3 min, out Vector3 max))
        {
            Matrix4x4 matrix = transform.ToMatrix();
            overlay.AddBox(min, max, matrix, SelectionColor, SelectionLineWidth);
        }

        Vector3 position = transform.Position;
        if (world.TryGet(selected, out PointLight point))
        {
            overlay.AddCircle(position, Vector3.UnitX, point.Range, SelectionColor, ReachLineWidth);
            overlay.AddCircle(position, Vector3.UnitY, point.Range, SelectionColor, ReachLineWidth);
            overlay.AddCircle(position, Vector3.UnitZ, point.Range, SelectionColor, ReachLineWidth);
        }

        if (world.TryGet(selected, out SpotLight spot))
        {
            DrawCone(overlay, transform, spot);
        }

        if (world.TryGet(selected, out IrradianceVolume volume))
        {
            Vector3 half = volume.Size / 2;
            Matrix4x4 moved = Matrix4x4.CreateTranslation(position);
            overlay.AddBox(-half, half, moved, SelectionColor, SelectionLineWidth);
        }
    }

    /// <summary>The spot light's outer cone along its -Z, out to its range.</summary>
    private static void DrawCone(OverlayBuilder overlay, in Transform transform, in SpotLight spot)
    {
        Vector3 apex = transform.Position;
        Vector3 direction = Vector3.Transform(-Vector3.UnitZ, transform.Rotation);
        float angle = float.DegreesToRadians(spot.OuterConeAngle);
        float radius = spot.Range * MathF.Tan(angle);
        Vector3 baseCentre = apex + (direction * spot.Range);
        overlay.AddCircle(baseCentre, direction, radius, SelectionColor, ReachLineWidth);
        OverlayBuilder.GetPlaneAxes(direction, out Vector3 first, out Vector3 second);
        for (int edge = 0; edge < ConeEdges; edge++)
        {
            float around = MathF.Tau * edge / ConeEdges;
            Vector3 offset = (first * MathF.Cos(around)) + (second * MathF.Sin(around));
            Vector3 rim = baseCentre + (offset * radius);
            overlay.AddLine(apex, rim, SelectionColor, ReachLineWidth);
        }
    }

    private void SetModeIf(bool chosen, GizmoMode mode)
    {
        if (chosen)
        {
            GizmoMode = mode;
        }
    }

    private void HandleSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectionText));
    }
}
