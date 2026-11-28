using System.Collections.ObjectModel;
using System.Numerics;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets.Buildings;
using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Buildings;

/// <summary>
/// The building creator in the viewport: its plan tools on the storey being edited (the rail's tools, their keys, the Tool tab), the
/// storeys above it hidden, labels of rooms, sizes and drags over the view, the top view, the picked shot's live view in the corner
/// and Look through, and Walk, which plays the building in a world of its own until Done.
/// </summary>
public sealed partial class BuildingPlanViewModel : ObservableObject, IDisposable
{
    private const float ShotLead = 14;
    private const string RailingSuffix = ".railing";
    private const string FloorSuffix = ".floor";

    private readonly EditorScene _scene;
    private readonly EditorCamera _camera;
    private readonly CreatorMode _creator;
    private (Vector3 Position, float Yaw, float Pitch, float Distance, CameraProjection Projection)? _beforeTop;
    private bool _labelsDirty = true;
    private int _width = 1;
    private int _height = 1;
    private double _scaling = 1;
    private Vector3 _labelCamera;
    private float _labelYaw;
    private float _labelPitch;
    private float _labelDistance;

    public BuildingPlanViewModel(EditorScene scene, EditorCamera camera, CreatorMode creator, BuildingPanelViewModel panel, UndoHistory history)
    {
        _scene = scene;
        _camera = camera;
        _creator = creator;
        Editor = new BuildingEditor(camera, GetPlace, GetFile, Change, history);
        Tool = new BuildingToolViewModel(Editor, GetFile, GetView, LookThrough);
        Editor.Changed += HandleEditorChanged;
        creator.Tool = Editor;
        Panel = panel;
    }

    public BuildingEditor Editor { get; }

    /// <summary>The Tool tab: the tool in hand, or the thing picked, and its settings.</summary>
    public BuildingToolViewModel Tool { get; }

    /// <summary>The labels over the view, placed for this frame's camera.</summary>
    public ObservableCollection<PlanLabelView> Labels { get; } = [];

    /// <summary>The snap steps offered in the mode bar, in metres.</summary>
    public IReadOnlyList<float> SnapSteps { get; } = [0.05f, 0.1f, 0.25f, 0.5f, 1];

    public float Snap
    {
        get => Editor.Snap;
        set
        {
            Editor.Snap = value;
            OnPropertyChanged();
        }
    }

    public bool SnapOn
    {
        get => Editor.SnapOn;
        set
        {
            Editor.SnapOn = value;
            OnPropertyChanged();
            Tool.Refresh();
        }
    }

    /// <summary>Whether the view looks straight down on the plan, orthographic (P).</summary>
    [ObservableProperty]
    public partial bool IsTopView { get; set; }

    /// <summary>The building's panel, opened again after an undo; the storey switcher shows its storeys.</summary>
    [ObservableProperty]
    public partial BuildingPanelViewModel? Panel { get; set; }

    private bool ShowsAbove => Panel?.ShowsAbove == true;

    /// <summary>The building being played by Walk, or null while it is edited.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWalking))]
    public partial BuildingWalk? Walk { get; private set; }

    public bool IsWalking => Walk is not null;

    /// <summary>The name of the shot Walk draws now.</summary>
    [ObservableProperty]
    public partial string LiveShot { get; private set; } = string.Empty;

    /// <summary>The picked shot's name, as the live view's header shows it; empty when no shot is picked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    public partial string PreviewName { get; private set; } = string.Empty;

    public bool HasPreview => PreviewName.Length > 0 && !IsWalking;

    /// <summary>The level of the storey being edited.</summary>
    public int Level => Editor.Level;

    /// <summary>Takes the tool from the rail or its key; Select picks.</summary>
    public void Choose(BuildingTool tool)
    {
        Editor.Tool = tool;
        OnPropertyChanged(nameof(Editor));
    }

    /// <summary>Edits the storey at the level.</summary>
    public void ShowLevel(int level)
    {
        Editor.Level = level;
        Panel?.ShowLevel(level);
        _labelsDirty = true;
    }

    /// <summary>Looks straight down on the plan, orthographic, or back to the view before.</summary>
    [RelayCommand]
    public void ToggleTopView() => IsTopView = !IsTopView;

    partial void OnIsTopViewChanged(bool value)
    {
        if (value)
        {
            _beforeTop = (_camera.Pivot, _camera.Yaw, _camera.Pitch, _camera.PivotDistance, _camera.Projection);
            BuildingFile? file = GetFile();
            float across = 12;
            if (file is not null)
            {
                across = Math.Max(file.Size.X, file.Size.Z) * 1.6f;
            }

            Vector3 middle = Vector3.Transform(new Vector3(0, file?.GetFloorHeight(Level) ?? 0, 0), GetPlace());
            float distance = across / (2 * MathF.Tan(float.DegreesToRadians(_camera.FieldOfView) / 2));
            _camera.Projection = CameraProjection.Orthographic;
            _camera.LookAt(middle, 0, -EditorCamera.PitchLimit, distance);
        }
        else if (_beforeTop is { } before)
        {
            _camera.Projection = before.Projection;
            _camera.LookAt(before.Position, before.Yaw, before.Pitch, before.Distance);
            _beforeTop = null;
        }

        _labelsDirty = true;
    }

    partial void OnPanelChanged(BuildingPanelViewModel? oldValue, BuildingPanelViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.LevelChanged -= ShowLevel;
        }

        if (newValue is not null)
        {
            newValue.LevelChanged += ShowLevel;
            newValue.ShowLevel(Editor.Level);
        }

        _labelsDirty = true;
        Tool.Refresh();
    }

    /// <summary>Plays the building from outside its first door, or stops playing it.</summary>
    [RelayCommand]
    public void ToggleWalk()
    {
        if (Walk is { } playing)
        {
            Walk = null;
            playing.Dispose();
            return;
        }

        if (GetFile() is not { } file || Panel is not { } panel)
        {
            return;
        }

        Editor.Apply();
        Walk = new BuildingWalk(_scene.Content, panel.Document.Path, file);
    }

    partial void OnWalkChanged(BuildingWalk? value)
    {
        OnPropertyChanged(nameof(HasPreview));
        Labels.Clear();
        _labelsDirty = true;
    }

    /// <summary>A click in the viewport while walking: the walker goes there, or to the door or button and uses it.</summary>
    public void Click(Vector2 pixel, int width, int height)
    {
        if (Walk is not { } walk)
        {
            return;
        }

        Ray ray = _camera.GetRay(pixel, width, height);
        if (walk.View is { } view)
        {
            CameraView drawn = CameraView.Create(view.Camera, view.Transform, width, height);
            ray = Ray.FromViewProjection(drawn.ViewProjection, pixel, width, height);
        }

        walk.Click(ray);
    }

    /// <summary>Runs once a frame: the walk, the storeys shown, the picked shot and the labels for the view's camera.</summary>
    public void Update(float seconds, int width, int height, double scaling)
    {
        _width = Math.Max(width, 1);
        _height = Math.Max(height, 1);
        if (Walk is { } walk)
        {
            walk.Update(seconds);
            LiveShot = walk.LiveShot;
            return;
        }

        if (Math.Abs(scaling - _scaling) > 1e-6)
        {
            _scaling = scaling;
            _labelsDirty = true;
        }

        ShowStoreys();
        if (_camera.Position != _labelCamera || _camera.Yaw != _labelYaw || _camera.Pitch != _labelPitch || _camera.PivotDistance != _labelDistance)
        {
            _labelsDirty = true;
        }

        if (_labelsDirty)
        {
            PlaceLabels();
        }
    }

    /// <summary>The world Walk plays while walking, else null.</summary>
    public World? WalkWorld => Walk?.World;

    /// <summary>The camera Walk draws with: the live shot's view.</summary>
    public RenderCamera? WalkView => Walk?.View;

    /// <summary>The picked shot's live view, for the viewport's corner.</summary>
    public RenderCamera? GetPreview()
    {
        if (IsWalking || Editor.Picked is not { Kind: PlanPickKind.Shot } pick || GetFile() is not { } file || file.Shots.Find(shot => shot.Id == pick.Id) is not { } shot)
        {
            return null;
        }

        return GetShotView(shot);
    }

    /// <summary>Puts the editor's view where the picked shot looks from, as wide as it is.</summary>
    [RelayCommand]
    public void LookThroughPicked()
    {
        if (Editor.Picked is { Kind: PlanPickKind.Shot } pick && GetFile() is { } file && file.Shots.Find(shot => shot.Id == pick.Id) is { } shot)
        {
            LookThrough(shot);
        }
    }

    public void Dispose()
    {
        Editor.Changed -= HandleEditorChanged;
        _creator.Tool = null;
        Walk?.Dispose();
        Walk = null;
        ShowAll();
    }

    private void LookThrough(BuildingShot shot)
    {
        RenderCamera view = GetShotView(shot);
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, view.Transform.Rotation);
        float yaw = MathF.Atan2(-forward.X, -forward.Z);
        float pitch = MathF.Asin(Math.Clamp(forward.Y, -1, 1));
        float distance = shot.Size / (2 * MathF.Tan(float.DegreesToRadians(_camera.FieldOfView) / 2));
        Vector3 pivot = view.Transform.Position + (forward * distance);
        IsTopView = false;
        _camera.Projection = CameraProjection.Orthographic;
        _camera.LookAt(pivot, yaw, pitch, distance);
    }

    private RenderCamera GetShotView(BuildingShot shot)
    {
        Matrix4x4 place = GetPlace();
        Vector3 position = Vector3.Transform(shot.Position, place);
        Quaternion turn = Quaternion.CreateFromRotationMatrix(place);
        Quaternion rotation = Quaternion.Normalize(turn * shot.Rotation);
        Camera lens = new() { Projection = CameraProjection.Orthographic, OrthographicHeight = Math.Max(shot.Size, 0.5f), FarPlane = ShotLead * 20 };
        Transform pose = new() { Position = position, Rotation = rotation };
        return new RenderCamera(lens, pose);
    }

    private void HandleEditorChanged()
    {
        _labelsDirty = true;
        string name = string.Empty;
        if (Editor.Picked is { Kind: PlanPickKind.Shot } pick && GetFile()?.Shots.Find(shot => shot.Id == pick.Id) is { } shot)
        {
            name = shot.Name;
        }

        PreviewName = name;
        if (Panel is { } panel && panel.Level != Editor.Level)
        {
            panel.ShowLevel(Editor.Level);
        }

        SelectPickedPart();
    }

    /// <summary>The part a pick stands for is selected too, so Mesh (Tab) shapes it.</summary>
    private void SelectPickedPart()
    {
        if (Panel is not { } panel || Editor.Picked is not { } pick)
        {
            return;
        }

        BuildingNode? node = panel.Nodes.FirstOrDefault(found => found.Pick == pick && found.PartId is not null);
        if (node?.PartId is { } part && _creator.SelectedPart != part)
        {
            _creator.SelectPart(part);
        }
    }

    private void PlaceLabels()
    {
        _labelsDirty = false;
        _labelCamera = _camera.Position;
        _labelYaw = _camera.Yaw;
        _labelPitch = _camera.Pitch;
        _labelDistance = _camera.PivotDistance;
        Labels.Clear();
        Vector3 eye = _camera.Forward;
        foreach (PlanLabel label in Editor.GetLabels())
        {
            Vector3 toLabel = label.Point - _camera.Position;
            if (_camera.Projection == CameraProjection.Perspective && Vector3.Dot(toLabel, eye) <= 0)
            {
                continue;
            }

            Vector2 pixel = _camera.Project(label.Point, _width, _height);
            double x = pixel.X / _scaling;
            double y = pixel.Y / _scaling;
            Labels.Add(new PlanLabelView(x, y, label.Title, label.Detail, label.Kind));
        }
    }

    /// <summary>
    /// Hides the parts of the storeys above the one edited, the roof, and the stairs, call buttons and shots on those storeys, unless they
    /// are shown; the parts made again are hidden too.
    /// </summary>
    private void ShowStoreys()
    {
        if (GetFile() is not { } file || _scene.Game.Services.GetService<CreationParts>() is not { } parts || !_scene.Game.World.IsAlive(_creator.Root))
        {
            return;
        }

        int level = Editor.Level;
        bool roofPicked = Editor.Picked is { Kind: PlanPickKind.Roof } || Editor.Tool == BuildingTool.Roof;
        bool open = Editor.ShowsOpenStorey;
        string levelId = BuildingFile.GetStoreyId(level);
        List<Entity> hide = [];
        List<Entity> show = [];
        foreach (Entity entity in parts.FindAll(_creator.Root))
        {
            if (!_scene.Game.World.TryGet(entity, out CreationPart part))
            {
                continue;
            }

            bool hidden = IsHiddenPart(file, part.Id, level, levelId, roofPicked, open);
            bool shown = !_scene.IsHidden(entity);
            if (hidden && shown)
            {
                hide.Add(entity);
            }
            else if (!hidden && !shown)
            {
                show.Add(entity);
            }
        }

        if (hide.Count > 0)
        {
            _scene.SetHidden(hide, true);
        }

        if (show.Count > 0)
        {
            _scene.SetHidden(show, false);
        }
    }

    /// <summary>
    /// Whether the part is hidden in the plan: the roof unless it is picked (in Parts or with its tool), when the whole building stands
    /// under it; what stands above the level unless the storeys above are shown; and, while stairs or an elevator are placed, all of
    /// the level but its floor, as the tool draws its walls as lines on the floor and what is cut above.
    /// </summary>
    private bool IsHiddenPart(BuildingFile file, string id, int level, string levelId, bool roofPicked, bool open)
    {
        if (id == BuildingFile.RoofId)
        {
            return !roofPicked;
        }

        if (roofPicked)
        {
            return false;
        }

        if (open)
        {
            if (id.StartsWith(levelId + ".", StringComparison.Ordinal) && id != levelId + FloorSuffix)
            {
                return true;
            }

            return IsAbove(file, id, level);
        }

        return !ShowsAbove && IsAbove(file, id, level);
    }

    /// <summary>Whether the part with the id stands above the level: on a storey over it, the roof, or stairs, a railing, a call button or a shot there.</summary>
    private static bool IsAbove(BuildingFile file, string id, int level)
    {
        if (id == BuildingFile.RoofId)
        {
            return true;
        }

        foreach (BuildingStorey storey in file.Storeys)
        {
            string storeyId = BuildingFile.GetStoreyId(storey.Level);
            if (id == storeyId || id.StartsWith(storeyId + ".", StringComparison.Ordinal))
            {
                return storey.Level > level;
            }
        }

        foreach (BuildingStairs stairs in file.Stairs)
        {
            string stairsId = BuildingFile.GetStairsId(stairs);
            if (id == stairsId + RailingSuffix)
            {
                return stairs.Level + 1 > level;
            }

            if (id == stairsId || id.StartsWith(stairsId + ".", StringComparison.Ordinal))
            {
                return stairs.Level > level;
            }
        }

        foreach (BuildingShot shot in file.Shots)
        {
            string shotId = BuildingFile.GetShotId(shot);
            if (id == shotId || id.StartsWith(shotId + ".", StringComparison.Ordinal))
            {
                return shot.Level > level;
            }
        }

        foreach (BuildingElevator elevator in file.Elevators)
        {
            if (id == BuildingFile.GetElevatorId(elevator) + RailingSuffix)
            {
                return elevator.To > level;
            }

            for (int stop = level + 1; stop <= elevator.To; stop++)
            {
                if (id == BuildingFile.GetCallId(elevator, stop))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void ShowAll()
    {
        if (_scene.Game.Services.GetService<CreationParts>() is not { } parts || !_scene.Game.World.IsAlive(_creator.Root))
        {
            return;
        }

        IReadOnlyList<Entity> all = parts.FindAll(_creator.Root);
        List<Entity> hidden = [.. all.Where(_scene.IsHidden)];
        if (hidden.Count > 0)
        {
            _scene.SetHidden(hidden, false);
        }
    }

    private Matrix4x4 GetPlace()
    {
        World world = _scene.Game.World;
        if (!world.IsAlive(_creator.Root) || !world.TryGet(_creator.Root, out Transform transform))
        {
            return Matrix4x4.Identity;
        }

        return transform.ToMatrix();
    }

    private BuildingFile? GetFile() => Panel?.Document.Building;

    private void Change(string name, Action<BuildingFile> edit)
    {
        if (Panel is not { } panel)
        {
            return;
        }

        panel.Document.Change(edit);
        panel.Refresh();
    }

    private (Vector3 Position, Quaternion Rotation)? GetView()
    {
        Matrix4x4 place = GetPlace();
        if (!Matrix4x4.Invert(place, out Matrix4x4 inverse))
        {
            return null;
        }

        Vector3 position = Vector3.Transform(_camera.Position, inverse);
        Quaternion turn = Quaternion.CreateFromRotationMatrix(inverse);
        Quaternion rotation = Quaternion.Normalize(turn * _camera.Rotation);
        return (position, rotation);
    }
}
