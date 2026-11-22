using System.Collections.ObjectModel;
using System.Numerics;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Shots;

/// <summary>
/// The timeline of the selected shot, under the viewport: its keys along a time axis, retimed by dragging, a scrubbed time the
/// camera preview shows, keys added from the editor's view and deleted, and the selected key, whose world transform the gizmo
/// moves and turns. Every edit goes through <see cref="EditorScene"/>.
/// </summary>
public sealed partial class ShotTimelineViewModel : ObservableObject
{
    /// <summary>How close, in pixels, a click must come to a key's handle to pick it.</summary>
    public const float KeyPickPixels = 10;

    private const int RailSamplesPerMetre = 8;

    private static readonly QueryDescription Targets = new QueryDescription().WithAll<CameraTarget, Transform>();
    private static readonly QueryDescription Named = new QueryDescription().WithAll<EntityName, Transform>();

    private readonly EditorScene _scene;

    public ShotTimelineViewModel(EditorScene scene)
    {
        _scene = scene;
        scene.Selection.Changed += HandleSelectionChanged;
        scene.Edited += Refresh;
        scene.Opened += HandleSelectionChanged;
    }

    /// <summary>The selected shot, or null when the selection is no shot.</summary>
    public Entity? Shot { get; private set; }

    public bool HasShot => Shot is not null;

    public ObservableCollection<ShotKeyItem> Keys { get; } = [];

    /// <summary>The time of the last key, or 1 for a timeline of no length, so the axis has room.</summary>
    public float Duration { get; private set; } = 1;

    [ObservableProperty]
    public partial float ScrubTime { get; set; }

    [ObservableProperty]
    public partial int? SelectedKey { get; private set; }

    public void SelectKey(int? key)
    {
        if (key is { } index && (index < 0 || index >= Keys.Count))
        {
            key = null;
        }

        SelectedKey = key;
        foreach (ShotKeyItem item in Keys)
        {
            item.IsSelected = item.Index == key;
        }
    }

    /// <summary>Retimes a key, kept between its neighbours' times and never before 0.</summary>
    public void MoveKeyTime(int key, float time)
    {
        if (!TryGetShot(out Entity entity, out Shot shot) || key < 0 || key >= shot.Keys.Count)
        {
            return;
        }

        List<ShotKey> keys = CloneKeys(shot.Keys);
        float earliest = 0;
        if (key > 0)
        {
            earliest = keys[key - 1].Time;
        }

        float latest = float.MaxValue;
        if (key < keys.Count - 1)
        {
            latest = keys[key + 1].Time;
        }

        keys[key].Time = Math.Clamp(time, earliest, latest);
        Write(entity, shot, keys);
    }

    /// <summary>Adds a key at the scrubbed time where a view (such as the editor camera's) stands, in time order, and selects it.</summary>
    public void AddKey(in Transform view)
    {
        if (!TryGetShot(out Entity entity, out Shot shot))
        {
            return;
        }

        Transform transform = _scene.Game.World.Get<Transform>(entity);
        ShotPose local = ShotPose.FromWorld(view, transform, 0);
        ShotKey key = new() { Position = local.Position, Rotation = local.Rotation, Time = ScrubTime };
        List<ShotKey> keys = CloneKeys(shot.Keys);
        int index = keys.FindIndex(existing => existing.Time > ScrubTime);
        if (index < 0)
        {
            index = keys.Count;
        }

        keys.Insert(index, key);
        Write(entity, shot, keys);
        SelectKey(index);
    }

    [RelayCommand]
    public void DeleteSelectedKey()
    {
        if (SelectedKey is not { } index || !TryGetShot(out Entity entity, out Shot shot) || index >= shot.Keys.Count)
        {
            return;
        }

        List<ShotKey> keys = CloneKeys(shot.Keys);
        keys.RemoveAt(index);
        SelectKey(null);
        Write(entity, shot, keys);
    }

    /// <summary>The selected key's pose in the world, for the gizmo.</summary>
    public bool TryGetKeyTransform(out Transform world)
    {
        world = default;
        if (SelectedKey is not { } index || !TryGetShot(out Entity entity, out Shot shot) || index >= shot.Keys.Count)
        {
            return false;
        }

        ShotKey key = shot.Keys[index];
        Transform transform = _scene.Game.World.Get<Transform>(entity);
        ShotPose local = new(key.Position, key.Rotation, key.Zoom);
        RenderCamera view = local.ToWorld(transform, new Camera());
        world = view.Transform;
        return true;
    }

    /// <summary>Moves and turns the selected key to a pose in the world, as the gizmo drags it.</summary>
    public void SetKeyTransform(in Transform world)
    {
        if (SelectedKey is not { } index || !TryGetShot(out Entity entity, out Shot shot) || index >= shot.Keys.Count)
        {
            return;
        }

        Transform transform = _scene.Game.World.Get<Transform>(entity);
        ShotPose local = ShotPose.FromWorld(world, transform, 0);
        List<ShotKey> keys = CloneKeys(shot.Keys);
        keys[index].Position = local.Position;
        keys[index].Rotation = local.Rotation;
        Write(entity, shot, keys);
    }

    /// <summary>The shot's camera as it looks at the scrubbed time, for the viewport's camera preview.</summary>
    public RenderCamera? GetPreview()
    {
        if (!TryGetShot(out Entity entity, out Shot shot))
        {
            return null;
        }

        Camera lens = GetLens(entity);
        ShotTimeline timeline = CreateTimeline(shot, lens);
        ShotPose local = timeline.Evaluate(ScrubTime);
        Transform transform = _scene.Game.World.Get<Transform>(entity);
        RenderCamera view = local.ToWorld(transform, lens);
        if (!shot.LookAtTarget || FindAim(shot.Target) is not { } aim)
        {
            return view;
        }

        Transform pose = view.Transform;
        pose.Rotation = ShotPose.LookAt(pose.Position, aim, pose.Rotation);
        return new RenderCamera(view.Camera, pose);
    }

    /// <summary>Where a shot's target is aimed at, as the game finds it: the entity named, else the first camera target, at its aim height.</summary>
    private Vector3? FindAim(string name)
    {
        World world = _scene.Game.World;
        Entity found = Entity.Null;
        if (string.IsNullOrEmpty(name))
        {
            world.Query(in Targets, (Entity entity) =>
            {
                if (found == Entity.Null || entity.Id < found.Id)
                {
                    found = entity;
                }
            });
        }
        else
        {
            world.Query(in Named, (Entity entity, ref EntityName entityName) =>
            {
                if (found == Entity.Null && entityName.Value == name)
                {
                    found = entity;
                }
            });
        }

        if (found == Entity.Null)
        {
            return null;
        }

        Vector3 aim = world.Get<Transform>(found).Position;
        if (world.TryGet(found, out CameraTarget target))
        {
            aim.Y += target.Height;
        }

        return aim;
    }

    /// <summary>Points along the shot's rail in the world, close enough together to draw it as lines.</summary>
    public IReadOnlyList<Vector3> GetRailPoints()
    {
        if (!TryGetShot(out Entity entity, out Shot shot))
        {
            return [];
        }

        Camera lens = GetLens(entity);
        ShotTimeline timeline = CreateTimeline(shot, lens);
        Matrix4x4 shotWorld = _scene.Game.World.Get<Transform>(entity).ToMatrix();
        float length = timeline.Rail.Length;
        int samples = Math.Max((int)MathF.Ceiling(length * RailSamplesPerMetre), 1);
        List<Vector3> points = [];
        for (int sample = 0; sample <= samples; sample++)
        {
            float distance = length * sample / samples;
            Vector3 local = timeline.Rail.GetPoint(distance);
            Vector3 point = Vector3.Transform(local, shotWorld);
            points.Add(point);
        }

        return points;
    }

    /// <summary>The keys' positions in the world, in order.</summary>
    public IReadOnlyList<Vector3> GetKeyPoints()
    {
        if (!TryGetShot(out Entity entity, out Shot shot))
        {
            return [];
        }

        Matrix4x4 shotWorld = _scene.Game.World.Get<Transform>(entity).ToMatrix();
        List<Vector3> points = [];
        foreach (ShotKey key in shot.Keys)
        {
            Vector3 point = Vector3.Transform(key.Position, shotWorld);
            points.Add(point);
        }

        return points;
    }

    /// <summary>The key whose handle is under the pixel, the nearest to the camera when several are, or null.</summary>
    public int? HitTestKey(EditorCamera camera, Vector2 pixel, int width, int height)
    {
        IReadOnlyList<Vector3> points = GetKeyPoints();
        int? nearest = null;
        float nearestDepth = float.PositiveInfinity;
        Vector3 eye = camera.Position;
        for (int index = 0; index < points.Count; index++)
        {
            Vector2 shown = camera.Project(points[index], width, height);
            if (Vector2.Distance(shown, pixel) > KeyPickPixels)
            {
                continue;
            }

            Vector3 toKey = points[index] - eye;
            float depth = Vector3.Dot(toKey, camera.Forward);
            bool inFront = depth > 0 || camera.Projection == CameraProjection.Orthographic;
            if (inFront && depth < nearestDepth)
            {
                nearest = index;
                nearestDepth = depth;
            }
        }

        return nearest;
    }

    /// <summary>Reads the selected shot's keys again, as after an edit.</summary>
    public void Refresh()
    {
        Keys.Clear();
        Duration = 1;
        if (!TryGetShot(out _, out Shot shot))
        {
            OnPropertyChanged(nameof(Duration));
            return;
        }

        float last = 0;
        if (shot.Keys.Count > 0)
        {
            last = shot.Keys[^1].Time;
        }

        if (last > 0)
        {
            Duration = last;
        }

        for (int index = 0; index < shot.Keys.Count; index++)
        {
            ShotKey key = shot.Keys[index];
            float fraction = key.Time / Duration;
            ShotKeyItem item = new(index, key.Time, fraction, key.Marker.Name) { IsSelected = index == SelectedKey };
            Keys.Add(item);
        }

        OnPropertyChanged(nameof(Duration));
    }

    private void HandleSelectionChanged()
    {
        Entity? selected = _scene.Selection.Selected;
        Entity? shot = null;
        if (selected is { } entity && IsShot(entity))
        {
            shot = entity;
        }

        if (shot != Shot)
        {
            Shot = shot;
            SelectedKey = null;
            ScrubTime = 0;
            OnPropertyChanged(nameof(Shot));
            OnPropertyChanged(nameof(HasShot));
        }

        Refresh();
    }

    private bool IsShot(Entity entity)
    {
        World world = _scene.Game.World;
        return world.IsAlive(entity) && world.Has<Shot>(entity) && world.Has<Transform>(entity);
    }

    private bool TryGetShot(out Entity entity, out Shot shot)
    {
        shot = default;
        entity = Entity.Null;
        if (Shot is not { } selected || !IsShot(selected))
        {
            return false;
        }

        entity = selected;
        shot = _scene.Game.World.Get<Shot>(selected);
        return true;
    }

    private Camera GetLens(Entity entity)
    {
        if (_scene.Game.World.TryGet(entity, out Camera camera))
        {
            return camera;
        }

        return new Camera();
    }

    private static ShotTimeline CreateTimeline(in Shot shot, in Camera lens)
    {
        float zoom = lens.OrthographicHeight;
        if (lens.Projection == CameraProjection.Perspective)
        {
            zoom = lens.FieldOfView;
        }

        return new ShotTimeline(shot.Keys, zoom);
    }

    /// <summary>Copies of the keys, so an edit never changes the list the world's component holds.</summary>
    private static List<ShotKey> CloneKeys(List<ShotKey> keys)
    {
        List<ShotKey> copies = [];
        foreach (ShotKey key in keys)
        {
            ShotKey copy = new()
            {
                Position = key.Position,
                Rotation = key.Rotation,
                Zoom = key.Zoom,
                Time = key.Time,
                Ease = key.Ease,
                Marker = key.Marker,
            };
            copies.Add(copy);
        }

        return copies;
    }

    private void Write(Entity entity, Shot shot, List<ShotKey> keys)
    {
        shot.Keys = keys;
        _scene.SetComponent(entity, shot);
    }
}
