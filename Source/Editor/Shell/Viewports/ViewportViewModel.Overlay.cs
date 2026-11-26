using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Editor.Core.Shots;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Viewports;

public sealed partial class ViewportViewModel
{
    /// <summary>
    /// Fills the overlay with this frame's icons, the selection's outline or reach, and the gizmo; in the creator only the selected
    /// part's outline.
    /// </summary>
    public void BuildOverlay(OverlayBuilder overlay, int viewportHeight)
    {
        overlay.Begin(Camera, viewportHeight);
        if (IsGameView)
        {
            return;
        }

        World world = Scene.Game.World;
        if (Creator is { } creator)
        {
            if (creator.FindSelectedPart() is { } part && world.TryGet(part, out Transform placed))
            {
                if (creator.MeshSelection is null)
                {
                    DrawSelection(overlay, world, part, placed);
                }
            }

            creator.DrawMesh(overlay);
            return;
        }

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

        ShotOverlay.Draw(overlay, world, Timeline);
        if (Selection.Selected is not { } selected || !world.IsAlive(selected))
        {
            return;
        }

        Transform transform = world.Get<Transform>(selected);
        if (Timeline.TryGetKeyTransform(out Transform key))
        {
            transform = key;
        }

        Gizmo.Draw(overlay, Camera, transform, viewportHeight);
    }

    /// <summary>
    /// The selected camera and where it looks from, shown live in a corner of the viewport, a shot where its timeline is scrubbed;
    /// none in the game view.
    /// </summary>
    public RenderCamera? GetPreviewCamera()
    {
        World world = Scene.Game.World;
        if (IsGameView || Selection.Selected is not { } selected || !world.IsAlive(selected) || !world.Has<Camera, Transform>(selected))
        {
            return null;
        }

        if (Timeline.HasShot)
        {
            return Timeline.GetPreview();
        }

        Camera camera = world.Get<Camera>(selected);
        Transform transform = world.Get<Transform>(selected);
        return new RenderCamera(camera, transform);
    }

    /// <summary>The depth fade the viewport draws: the drawn mood's, without any shot's mood; none while it is switched off.</summary>
    public DepthFade GetDepthFade()
    {
        if (!ShowDepthFade)
        {
            return default;
        }

        LightingMood drawn = GetDrawnMood();
        return drawn.ToDepthFade();
    }

    /// <summary>The depth fade the selected camera's preview draws: the drawn mood's, changed by the selected shot's mood.</summary>
    public DepthFade GetPreviewDepthFade()
    {
        if (!ShowDepthFade || Selection.Selected is not { } selected)
        {
            return default;
        }

        LightingMood drawn = GetDrawnMood();
        World world = Scene.Game.World;
        if (world.IsAlive(selected) && world.TryGet(selected, out Shot shot) && LoadMood(shot.Mood) is { } shotMood)
        {
            shotMood.Apply(drawn);
        }

        return drawn.ToDepthFade();
    }

    /// <summary>
    /// Where the depth fade is measured from, as the game does: the selected shot's named target, else the first camera target,
    /// at its aim height; without one, the editor camera's pivot.
    /// </summary>
    public Vector3 GetDepthFocus()
    {
        World world = Scene.Game.World;
        string target = string.Empty;
        if (Selection.Selected is { } selected && world.IsAlive(selected) && world.TryGet(selected, out Shot shot))
        {
            target = shot.Target;
        }

        Entity found = Entity.Null;
        if (string.IsNullOrEmpty(target))
        {
            world.Query(in CameraTargets, (Entity entity) =>
            {
                if (found == Entity.Null || entity.Id < found.Id)
                {
                    found = entity;
                }
            });
        }
        else
        {
            world.Query(in NamedEntities, (Entity entity, ref EntityName name) =>
            {
                if (found == Entity.Null && name.Value == target)
                {
                    found = entity;
                }
            });
        }

        if (found == Entity.Null)
        {
            return Camera.Pivot;
        }

        Vector3 aim = world.Get<Transform>(found).Position;
        if (world.TryGet(found, out CameraTarget cameraTarget))
        {
            aim.Y += cameraTarget.Height;
        }

        return aim;
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
}
