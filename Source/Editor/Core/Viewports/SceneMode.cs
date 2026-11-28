using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Gizmos;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Selection;
using FlickerVoid.Editor.Core.Shots;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Core.Viewports;

/// <summary>
/// The viewport's mode over the scene: a click drags the gizmo handle or the selected shot key under the mouse, or selects what is
/// there, icons first; dragging the gizmo moves the whole selection with it, as one undo step. The game view hides gizmos and icons,
/// so it selects meshes only.
/// </summary>
public sealed class SceneMode(EditorScene scene, EditorCamera camera, TransformGizmo gizmo, EditorIcons icons, ShotTimelineViewModel timeline)
    : IViewportMode
{
    private bool _draggingKey;

    /// <summary>Whether the viewport shows the world as the game will, without gizmos and icons.</summary>
    public bool IsGameView { get; set; }

    public UndoHistory History => scene.History;

    private EditorSelection Selection => scene.Selection;

    public bool Save() => scene.Save();

    /// <summary>
    /// A left click without Alt: starts dragging the gizmo handle under the mouse, or selects what is there; the game view hides
    /// gizmos and icons, so it selects meshes only.
    /// </summary>
    public void PointerPressed(Vector2 pixel, int width, int height)
    {
        World world = scene.Game.World;
        if (IsGameView)
        {
            SelectAt(pixel, width, height);
            return;
        }

        if (PressShotKey(pixel, width, height))
        {
            return;
        }

        if (Selection.Selected is { } selected && world.IsAlive(selected))
        {
            Transform transform = world.Get<Transform>(selected);
            if (gizmo.BeginDrag(camera, transform, pixel, width, height))
            {
                List<Entity> moved = [.. Selection.Entities];
                string verb = gizmo.Mode.ToString();
                scene.History.BeginGroup(DescribeMany(world, verb, moved));
                return;
            }
        }

        SelectAt(pixel, width, height);
    }

    /// <summary>Selects what is under the mouse: an icon first, then a mesh, or nothing; the game view has no icons.</summary>
    public void SelectAt(Vector2 pixel, int width, int height)
    {
        World world = scene.Game.World;
        Entity? picked = null;
        if (!IsGameView)
        {
            icons.Collect(world, Selection.Selected);
            picked = icons.HitTest(camera, pixel, width, height);
        }

        if (picked is null)
        {
            Ray ray = camera.GetRay(pixel, width, height);
            picked = scene.Picker.Pick(world, ray);
        }

        Selection.Select(picked);
    }

    public void PointerLeft() => gizmo.Leave();

    /// <summary>Drags the gizmo, or finds the handle under the mouse.</summary>
    public void PointerMoved(Vector2 pixel, int width, int height)
    {
        World world = scene.Game.World;
        if (Selection.Selected is not { } selected || !world.IsAlive(selected))
        {
            return;
        }

        if (gizmo.IsDragging && _draggingKey)
        {
            Transform draggedKey = gizmo.Drag(camera, pixel, width, height);
            timeline.SetKeyTransform(draggedKey);
            return;
        }

        if (gizmo.IsDragging)
        {
            Transform before = world.Get<Transform>(selected);
            Transform dragged = gizmo.Drag(camera, pixel, width, height);
            scene.SetComponent(selected, dragged);
            MoveOthers(world, selected, before, dragged);
            return;
        }

        if (IsGameView)
        {
            return;
        }

        Transform transform = world.Get<Transform>(selected);
        if (timeline.TryGetKeyTransform(out Transform key))
        {
            transform = key;
        }

        gizmo.Hover(camera, transform, pixel, width, height);
    }

    public void Update()
    {
    }

    public void PointerReleased()
    {
        if (gizmo.IsDragging)
        {
            scene.History.EndGroup();
        }

        gizmo.EndDrag();
        _draggingKey = false;
    }

    /// <summary>An undo step's name for a verb on entities: the one entity's name, or how many there are.</summary>
    public static string DescribeMany(World world, string verb, IReadOnlyList<Entity> entities)
    {
        if (entities.Count == 1 && world.IsAlive(entities[0]) && world.TryGet(entities[0], out EntityName name))
        {
            return $"{verb} {name.Value}";
        }

        return $"{verb} {entities.Count} entities";
    }

    /// <summary>
    /// With a shot selected, starts dragging the gizmo on its selected key, or selects the key under the mouse; true when the
    /// press was the key's. Anything else leaves the shot's keys unselected.
    /// </summary>
    private bool PressShotKey(Vector2 pixel, int width, int height)
    {
        if (!timeline.HasShot)
        {
            return false;
        }

        if (timeline.TryGetKeyTransform(out Transform key) && gizmo.BeginDrag(camera, key, pixel, width, height))
        {
            _draggingKey = true;
            return true;
        }

        int? hit = timeline.HitTestKey(camera, pixel, width, height);
        timeline.SelectKey(hit);
        return hit is not null;
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
            scene.SetComponent(other, transform);
        }
    }
}
