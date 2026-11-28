using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Engine.Cameras.Triggers;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Shots;

/// <summary>
/// Draws what directs the camera, which the game never shows: every trigger area's box, and the selected shot's rail with a
/// handle on each key and a short line along where it looks, the selected key in the selection colour.
/// </summary>
public static class ShotOverlay
{
    public const float RailWidth = OverlayLines.Bold;
    public const float KeyPixels = 5;

    private const float AreaWidth = OverlayLines.Thin;
    private const float LookLength = 0.6f;

    private static readonly QueryDescription Areas = new QueryDescription().WithAll<TriggerArea, Transform>().WithNone<Hidden>();
    private static readonly Vector4 RailColor = new(0.35f, 0.8f, 1, 1);
    private static readonly Vector4 AreaColor = new(0.55f, 0.9f, 0.45f, 0.8f);

    public static void Draw(OverlayBuilder overlay, World world, ShotTimelineViewModel timeline)
    {
        world.Query(in Areas, (ref TriggerArea area, ref Transform transform) =>
        {
            Vector3 half = area.Size * 0.5f;
            Vector3 min = area.Offset - half;
            Vector3 max = area.Offset + half;
            Matrix4x4 matrix = transform.ToMatrix();
            overlay.AddBox(min, max, matrix, AreaColor, AreaWidth);
        });

        if (!timeline.HasShot)
        {
            return;
        }

        IReadOnlyList<Vector3> rail = timeline.GetRailPoints();
        for (int point = 1; point < rail.Count; point++)
        {
            overlay.AddLine(rail[point - 1], rail[point], RailColor, RailWidth);
        }

        int? selectedKey = timeline.SelectedKey;
        IReadOnlyList<Vector3> keys = timeline.GetKeyPoints();
        for (int key = 0; key < keys.Count; key++)
        {
            Vector4 color = RailColor;
            if (key == selectedKey)
            {
                color = EditorIcons.SelectedColor;
            }

            overlay.AddCube(keys[key], KeyPixels, color);
        }

        if (timeline.TryGetKeyTransform(out Transform selected))
        {
            Vector3 forward = Vector3.Transform(-Vector3.UnitZ, selected.Rotation);
            Vector3 ahead = selected.Position + (forward * LookLength);
            overlay.AddLine(selected.Position, ahead, EditorIcons.SelectedColor, RailWidth);
        }
    }
}
