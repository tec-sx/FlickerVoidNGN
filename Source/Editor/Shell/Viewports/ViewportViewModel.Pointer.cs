using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Editor.Viewports;

public sealed partial class ViewportViewModel
{
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

        if (PressShotKey(pixel, width, height))
        {
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

    /// <summary>
    /// With a shot selected, starts dragging the gizmo on its selected key, or selects the key under the mouse; true when the
    /// press was the key's. Anything else leaves the shot's keys unselected.
    /// </summary>
    private bool PressShotKey(Vector2 pixel, int width, int height)
    {
        if (!Timeline.HasShot)
        {
            return false;
        }

        if (Timeline.TryGetKeyTransform(out Transform key) && Gizmo.BeginDrag(Camera, key, pixel, width, height))
        {
            _draggingKey = true;
            return true;
        }

        int? hit = Timeline.HitTestKey(Camera, pixel, width, height);
        Timeline.SelectKey(hit);
        return hit is not null;
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

        if (Gizmo.IsDragging && _draggingKey)
        {
            Transform draggedKey = Gizmo.Drag(Camera, pixel, width, height);
            Timeline.SetKeyTransform(draggedKey);
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
        if (Timeline.TryGetKeyTransform(out Transform key))
        {
            transform = key;
        }

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
        _draggingKey = false;
    }
}
