using System.Diagnostics;
using System.Numerics;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Editor.Core.Gizmos;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Selection;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Editor.Scenes;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

namespace FlickerVoid.Editor.Viewports;

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
    private const float FrustumAspect = 16f / 9f;

    /// <summary>The radius framed round an entity without a mesh, such as a light.</summary>
    private const float FramedPointRadius = 1;

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

    /// <summary>The light and grade the viewport draws the scene with: the default mood, or the mood file opened last.</summary>
    [ObservableProperty]
    public partial LightingMood Mood { get; set; } = new();

    public EditorSelection Selection => Scene.Selection;

    public TransformGizmo Gizmo { get; } = new();

    public EditorIcons Icons { get; } = new();

    /// <summary>Whether the viewport's renderer turns on the Vulkan validation layer.</summary>
    public bool Validation { get; }

    /// <summary>What the viewport shows on, or why it can't.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = "Starting the viewport.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGridVisible))]
    public partial bool ShowGrid { get; set; } = true;

    /// <summary>Shows the world as the game will, without the grid, icons, selection and gizmo.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGridVisible))]
    public partial bool IsGameView { get; set; }

    public bool IsGridVisible => ShowGrid && !IsGameView;

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

    /// <summary>Whether the handles follow the selected entity's own axes rather than the world's.</summary>
    public bool IsLocalSpace
    {
        get => Gizmo.Space == GizmoSpace.Local;
        set
        {
            GizmoSpace space = value ? GizmoSpace.Local : GizmoSpace.World;
            if (space == Gizmo.Space)
            {
                return;
            }

            Gizmo.Space = space;
            OnPropertyChanged();
        }
    }

    /// <summary>Moves the camera by one frame of input over a viewport the given number of pixels high.</summary>
    public void Update(in ViewportInput input, float seconds, int viewportHeight)
    {
        _controller.Apply(input, seconds, viewportHeight);
    }

    /// <summary>Q, W, E and R switch the gizmo, unless the right button is held, when they fly the camera; G toggles the game view.</summary>
    public void KeyPressed(ViewportKey key, ViewportButtons held)
    {
        if ((held & ViewportButtons.Right) != 0)
        {
            return;
        }

        switch (key)
        {
            case ViewportKey.Q:
                GizmoMode = GizmoMode.Select;
                break;
            case ViewportKey.W:
                GizmoMode = GizmoMode.Move;
                break;
            case ViewportKey.E:
                GizmoMode = GizmoMode.Rotate;
                break;
            case ViewportKey.R:
                GizmoMode = GizmoMode.Scale;
                break;
            case ViewportKey.G:
                IsGameView = !IsGameView;
                break;
            case ViewportKey.Delete:
                DeleteSelected();
                break;
            case ViewportKey.F:
                FrameSelected();
                break;
        }
    }

    /// <summary>
    /// Moves the camera, still looking the same way, so the selection fills the view (F): meshes by their boxes, anything else
    /// as a small sphere round its place. With nothing selected it frames every entity of the scene.
    /// </summary>
    [RelayCommand]
    public void FrameSelected()
    {
        World world = Scene.Game.World;
        IEnumerable<Entity> framed = Selection.Entities.Count > 0 ? Selection.Entities : Scene.Entities;
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        bool any = false;
        foreach (Entity entity in framed)
        {
            if (!world.IsAlive(entity) || !world.TryGet(entity, out Transform transform))
            {
                continue;
            }

            any = true;
            if (Scene.Picker.TryGetBounds(world, entity, out Vector3 low, out Vector3 high))
            {
                Matrix4x4 matrix = transform.ToMatrix();
                for (int corner = 0; corner < 8; corner++)
                {
                    float x = (corner & 1) == 0 ? low.X : high.X;
                    float y = (corner & 2) == 0 ? low.Y : high.Y;
                    float z = (corner & 4) == 0 ? low.Z : high.Z;
                    Vector3 point = Vector3.Transform(new Vector3(x, y, z), matrix);
                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                }
            }
            else
            {
                Vector3 room = new(FramedPointRadius);
                min = Vector3.Min(min, transform.Position - room);
                max = Vector3.Max(max, transform.Position + room);
            }
        }

        if (!any)
        {
            return;
        }

        Vector3 center = (min + max) / 2;
        float radius = Vector3.Distance(min, max) / 2;
        Camera.Frame(center, radius);
    }

    /// <summary>Adds a copy of each selected entity in the same place and selects the copies, to move them away with the gizmo.</summary>
    [RelayCommand]
    public void DuplicateSelected()
    {
        World world = Scene.Game.World;
        if (Scene.ScenePath is null)
        {
            return;
        }

        List<Entity> copies = [];
        Entity? primary = null;
        List<Entity> selected = [.. Selection.Entities.Where(world.IsAlive)];
        Scene.History.BeginGroup(DescribeMany("Duplicate", selected));
        foreach (Entity entity in selected)
        {
            Entity copy = Scene.DuplicateEntity(entity);
            copies.Add(copy);
            if (entity == Selection.Selected)
            {
                primary = copy;
            }
        }

        Scene.History.EndGroup();
        if (copies.Count > 0)
        {
            Selection.SelectAll(copies, primary);
        }
    }

    /// <summary>
    /// Adds a mesh entity drawing a model file where it is dropped: on the mesh under the mouse, else on the ground, else where the
    /// mouse's ray crosses the plane of the camera's pivot; then selects it.
    /// </summary>
    public void DropModel(string path, Vector2 pixel, int width, int height)
    {
        if (Scene.ScenePath is null)
        {
            Status = "Open a scene before adding models: double-click one in the content browser.";
            return;
        }

        World world = Scene.Game.World;
        Ray ray = Camera.GetRay(pixel, width, height);
        Vector3 position = Camera.Pivot;
        if (Scene.Picker.Pick(world, ray, out float distance) is not null)
        {
            position = ray.GetPoint(distance);
        }
        else if (ray.IntersectPlane(Vector3.Zero, Vector3.UnitY, out Vector3 ground))
        {
            position = ground;
        }
        else if (ray.IntersectPlane(Camera.Pivot, Camera.Forward, out Vector3 facing))
        {
            position = facing;
        }

        EntityPreset preset = EntityPreset.ForModel(path);
        EntityData data = preset.Create(world, Scene.Entities, position);
        Entity entity = Scene.AddEntity(data);
        Selection.Select(entity);
        Status = $"Added {data.Name}.";
    }

    /// <summary>Destroys the selected entities; the next save leaves them out of the scene file.</summary>
    [RelayCommand]
    public void DeleteSelected()
    {
        List<Entity> selected = [.. Selection.Entities];
        Scene.History.BeginGroup(DescribeMany("Delete", selected));
        foreach (Entity entity in selected)
        {
            Scene.DestroyEntity(entity);
        }

        Scene.History.EndGroup();
    }

    /// <summary>
    /// A left click without Alt: starts dragging the gizmo handle under the mouse, or selects what is there; the game view hides
    /// gizmos and icons, so it selects meshes only.
    /// </summary>
    public void PointerPressed(Vector2 pixel, int width, int height)
    {
        World world = Scene.Game.World;
        if (IsGameView)
        {
            SelectAt(pixel, width, height);
            return;
        }

        if (Selection.Selected is { } selected && world.IsAlive(selected))
        {
            Transform transform = world.Get<Transform>(selected);
            if (Gizmo.BeginDrag(Camera, transform, pixel, width, height))
            {
                List<Entity> moved = [.. Selection.Entities];
                string verb = Gizmo.Mode.ToString();
                Scene.History.BeginGroup(DescribeMany(verb, moved));
                return;
            }
        }

        SelectAt(pixel, width, height);
    }

    /// <summary>Selects what is under the mouse: an icon first, then a mesh, or nothing; the game view has no icons.</summary>
    public void SelectAt(Vector2 pixel, int width, int height)
    {
        World world = Scene.Game.World;
        Entity? picked = null;
        if (!IsGameView)
        {
            Icons.Collect(world, Selection.Selected);
            picked = Icons.HitTest(Camera, pixel, width, height);
        }

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

        if (Gizmo.IsDragging)
        {
            Transform before = world.Get<Transform>(selected);
            Transform dragged = Gizmo.Drag(Camera, pixel, width, height);
            Scene.SetComponent(selected, dragged);
            MoveOthers(world, selected, before, dragged);
            return;
        }

        if (IsGameView)
        {
            return;
        }

        Transform transform = world.Get<Transform>(selected);
        Gizmo.Hover(Camera, transform, pixel, width, height);
    }

    /// <summary>Moves the rest of the selection with the gizmo's entity: by its move, turned about it and scaled by its scaling.</summary>
    private void MoveOthers(World world, Entity selected, in Transform before, in Transform after)
    {
        Quaternion turn = after.Rotation * Quaternion.Inverse(before.Rotation);
        Vector3 scaling = Vector3.One;
        if (before.Scale.X != 0 && before.Scale.Y != 0 && before.Scale.Z != 0)
        {
            scaling = after.Scale / before.Scale;
        }

        foreach (Entity other in Selection.Entities)
        {
            if (other == selected || !world.IsAlive(other) || !world.Has<Transform>(other))
            {
                continue;
            }

            Transform transform = world.Get<Transform>(other);
            Vector3 offset = transform.Position - before.Position;
            Vector3 turned = Vector3.Transform(offset * scaling, turn);
            transform.Position = after.Position + turned;
            transform.Rotation = Quaternion.Normalize(turn * transform.Rotation);
            transform.Scale *= scaling;
            Scene.SetComponent(other, transform);
        }
    }

    public void PointerReleased()
    {
        if (Gizmo.IsDragging)
        {
            Scene.History.EndGroup();
        }

        Gizmo.EndDrag();
    }

    /// <summary>An undo step's name for entities: "Move Lamp" for one, "Move 3 entities" for more.</summary>
    private string DescribeMany(string verb, IReadOnlyList<Entity> entities)
    {
        World world = Scene.Game.World;
        if (entities.Count == 1 && world.IsAlive(entities[0]) && world.TryGet(entities[0], out EntityName name))
        {
            return $"{verb} {name.Value}";
        }

        return $"{verb} {entities.Count} entities";
    }

    /// <summary>Fills the overlay with this frame's icons, the selection's outline or reach, and the gizmo.</summary>
    public void BuildOverlay(OverlayBuilder overlay, int viewportHeight)
    {
        overlay.Begin(Camera, viewportHeight);
        if (IsGameView)
        {
            return;
        }

        World world = Scene.Game.World;
        Icons.Collect(world, Selection.Selected);
        foreach (IconInstance icon in Icons.Icons)
        {
            overlay.AddIcon(icon.Position, icon.Icon, icon.Color);
        }

        foreach (Entity entity in Selection.Entities)
        {
            if (world.IsAlive(entity) && world.TryGet(entity, out Transform shown))
            {
                DrawSelection(overlay, world, entity, shown);
            }
        }

        if (Selection.Selected is not { } selected || !world.IsAlive(selected))
        {
            return;
        }

        Transform transform = world.Get<Transform>(selected);
        Gizmo.Draw(overlay, Camera, transform, viewportHeight);
    }

    /// <summary>The selected camera and where it looks from, shown live in a corner of the viewport; none in the game view.</summary>
    public RenderCamera? GetPreviewCamera()
    {
        World world = Scene.Game.World;
        if (IsGameView || Selection.Selected is not { } selected || !world.IsAlive(selected) || !world.Has<Camera, Transform>(selected))
        {
            return null;
        }

        Camera camera = world.Get<Camera>(selected);
        Transform transform = world.Get<Transform>(selected);
        return new RenderCamera(camera, transform);
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

        if (world.TryGet(selected, out FlickerVoid.Engine.Rendering.Cameras.Camera camera))
        {
            DrawFrustum(overlay, transform, camera);
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

    /// <summary>The camera's view volume along its -Z, from its near to its far plane, framed at a 16:9 aspect.</summary>
    private static void DrawFrustum(OverlayBuilder overlay, in Transform transform, in Camera camera)
    {
        Span<Vector3> near = stackalloc Vector3[4];
        Span<Vector3> far = stackalloc Vector3[4];
        GetFrustumCorners(camera, camera.NearPlane, near);
        GetFrustumCorners(camera, camera.FarPlane, far);
        for (int corner = 0; corner < 4; corner++)
        {
            near[corner] = transform.Position + Vector3.Transform(near[corner], transform.Rotation);
            far[corner] = transform.Position + Vector3.Transform(far[corner], transform.Rotation);
        }

        for (int corner = 0; corner < 4; corner++)
        {
            int next = (corner + 1) % 4;
            overlay.AddLine(near[corner], near[next], SelectionColor, ReachLineWidth);
            overlay.AddLine(far[corner], far[next], SelectionColor, ReachLineWidth);
            overlay.AddLine(near[corner], far[corner], SelectionColor, ReachLineWidth);
        }
    }

    /// <summary>The corners of the camera's view at a distance, in its own space, going round.</summary>
    private static void GetFrustumCorners(in Camera camera, float distance, Span<Vector3> corners)
    {
        float halfHeight = camera.Projection == CameraProjection.Perspective
            ? distance * MathF.Tan(float.DegreesToRadians(camera.FieldOfView) / 2)
            : camera.OrthographicHeight / 2;
        float halfWidth = halfHeight * FrustumAspect;
        corners[0] = new Vector3(-halfWidth, -halfHeight, -distance);
        corners[1] = new Vector3(halfWidth, -halfHeight, -distance);
        corners[2] = new Vector3(halfWidth, halfHeight, -distance);
        corners[3] = new Vector3(-halfWidth, halfHeight, -distance);
    }

    /// <summary>Saves the world into its scene file.</summary>
    [RelayCommand]
    public void Save()
    {
        if (!Scene.Save())
        {
            Status = "The world has no scene file to save into; open one with --scene.";
            return;
        }

        Status = $"Saved {Scene.ScenePath}.";
    }

    /// <summary>Bakes every irradiance volume's probes with the viewport's mood, on a device of its own; the viewport reloads them.</summary>
    [RelayCommand]
    public void BakeProbes()
    {
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            GraphicsDeviceOptions options = new() { Validation = Validation };
            using VulkanGraphicsDevice device = new(options, Scene.Game.LoggerFactory);
            IReadOnlyList<string> baked = SceneProbes.Bake(device, Scene, Mood);
            if (baked.Count == 0)
            {
                Status = "No irradiance volumes to bake.";
                return;
            }

            string files = string.Join(", ", baked);
            Status = $"Baked {files} in {watch.Elapsed.TotalSeconds:0.0} s.";
        }
        catch (GraphicsUnavailableException exception)
        {
            Status = exception.Message;
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
