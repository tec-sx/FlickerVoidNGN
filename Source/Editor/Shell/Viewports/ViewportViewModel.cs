using System.Diagnostics;
using System.Numerics;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Gizmos;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Selection;
using FlickerVoid.Editor.Core.Shots;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Vehicles;

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

    private static readonly QueryDescription CameraTargets = new QueryDescription().WithAll<CameraTarget, Transform>();

    private static readonly QueryDescription NamedEntities = new QueryDescription().WithAll<EntityName, Transform>();

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
        Timeline = new ShotTimelineViewModel(scene);
        SceneMode = new SceneMode(scene, Camera, Gizmo, Icons, Timeline);
        Mode = SceneMode;
        MeshTools = new MeshToolsViewModel(() => Creator, message => Status = message);
        Selection.Changed += HandleSelectionChanged;
        scene.Content.Changed += HandleContentChanged;
        scene.Opened += HandleSceneOpened;
    }

    public EditorScene Scene { get; }

    public EditorCamera Camera { get; } = new();

    /// <summary>The light and grade the viewport draws the scene with: the default mood, or the mood file opened last.</summary>
    [ObservableProperty]
    public partial LightingMood Mood { get; set; } = new();

    public EditorSelection Selection => Scene.Selection;

    public TransformGizmo Gizmo { get; } = new();

    public EditorIcons Icons { get; } = new();

    /// <summary>The selected shot's timeline and keys, shown under the viewport while a shot is selected.</summary>
    public ShotTimelineViewModel Timeline { get; }

    /// <summary>Clicks and drags on the scene: its selection, gizmo and shot keys.</summary>
    public SceneMode SceneMode { get; }

    /// <summary>What clicks and drags work on now: the scene's mode unless another is open.</summary>
    public IViewportMode Mode { get; private set; }

    /// <summary>The tool bar of the creator's mesh mode.</summary>
    public MeshToolsViewModel MeshTools { get; private set; }

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

    partial void OnIsGameViewChanged(bool value) => SceneMode.IsGameView = value;

    /// <summary>Draws the canvas the scene is painted on; off shows the scene without it, in the viewport only.</summary>
    [ObservableProperty]
    public partial bool ShowCanvas { get; set; } = true;

    /// <summary>Draws the main camera's depth fade, in the viewport and the camera preview.</summary>
    [ObservableProperty]
    public partial bool ShowDepthFade { get; set; } = true;

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
        Mode.Update();
    }

    /// <summary>
    /// Q, W, E and R switch the gizmo, unless the right button is held, when they fly the camera; G toggles the game view; Escape
    /// leaves the creator.
    /// </summary>
    public void KeyPressed(ViewportKey key, ViewportButtons held, bool ctrl = false)
    {
        if ((held & ViewportButtons.Right) != 0)
        {
            return;
        }

        if (Creator is not null && PressMeshKey(key, ctrl))
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
            case ViewportKey.Escape:
                LeaveCreator();
                break;
        }
    }

    /// <summary>
    /// Moves the camera, still looking the same way, so the selection fills the view (F): meshes by their boxes, anything else
    /// as a small sphere round its place. With nothing selected it frames every entity of the scene, and in the creator the creation.
    /// </summary>
    [RelayCommand]
    public void FrameSelected()
    {
        World world = Scene.Game.World;
        IEnumerable<Entity> framed = Selection.Entities.Count > 0 ? Selection.Entities : Scene.Entities;
        if (Creator is { } creator)
        {
            framed = [creator.Root];
        }

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

    /// <summary>
    /// Adds a copy of each selected entity, with what is nested under it, in the same place and selects the copies, to move them away
    /// with the gizmo; a selected entity nested under another selected one is copied with it, once.
    /// </summary>
    [RelayCommand]
    public void DuplicateSelected()
    {
        World world = Scene.Game.World;
        if (Scene.ScenePath is null || IsCreating)
        {
            return;
        }

        List<Entity> copies = [];
        Entity? primary = null;
        List<Entity> alive = [.. Selection.Entities.Where(world.IsAlive)];
        HashSet<Entity> chosen = [.. alive];
        List<Entity> selected = [.. alive.Where(entity => !IsNestedUnderAny(entity, chosen))];
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

    private bool IsNestedUnderAny(Entity entity, HashSet<Entity> entities)
    {
        Entity? parent = Scene.GetParent(entity);
        while (parent is { } current)
        {
            if (entities.Contains(current))
            {
                return true;
            }

            parent = Scene.GetParent(current);
        }

        return false;
    }

    /// <summary>
    /// Adds an entity for a file where it is dropped, a vehicle for a vehicle file and else a mesh entity drawing a model: on the mesh
    /// under the mouse, else on the ground, else where the mouse's ray crosses the plane of the camera's pivot; then selects it.
    /// </summary>
    public void DropAsset(string path, Vector2 pixel, int width, int height)
    {
        if (Scene.ScenePath is null)
        {
            Status = "Open a scene before adding models or vehicles: double-click one in the content browser.";
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
        if (path.EndsWith(VehicleFile.Extension, StringComparison.OrdinalIgnoreCase))
        {
            preset = EntityPreset.ForVehicle(path);
        }

        EntityData data = preset.Create(world, Scene.Entities, position);
        Entity entity = Scene.AddEntity(data);
        Selection.Select(entity);
        Status = $"Added {data.Name}.";
    }

    /// <summary>Destroys the selected entities; the next save leaves them out of the scene file. The creator never edits the level.</summary>
    [RelayCommand]
    public void DeleteSelected()
    {
        if (IsCreating)
        {
            return;
        }

        List<Entity> selected = [.. Selection.Entities];
        Scene.History.BeginGroup(DescribeMany("Delete", selected));
        foreach (Entity entity in selected)
        {
            Scene.DestroyEntity(entity);
        }

        Scene.History.EndGroup();
    }

    /// <summary>Adds a key to the selected shot where the editor's view stands, at the timeline's scrubbed time.</summary>
    [RelayCommand]
    public void AddShotKey()
    {
        Transform view = Camera.ToTransform();
        Timeline.AddKey(view);
    }

    /// <summary>An undo step's name for entities: "Move Lamp" for one, "Move 3 entities" for more.</summary>
    private string DescribeMany(string verb, IReadOnlyList<Entity> entities)
    {
        return SceneMode.DescribeMany(Scene.Game.World, verb, entities);
    }

    /// <summary>Saves the world into its scene file, or in the creator the creation's file.</summary>
    [RelayCommand]
    public void Save()
    {
        if (CreatorFile is { } file)
        {
            Mode.Save();
            Status = $"Saved {file}.";
            return;
        }

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
            LightingMood drawn = GetDrawnMood();
            IReadOnlyList<string> baked = SceneProbes.Bake(device, Scene, drawn);
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
