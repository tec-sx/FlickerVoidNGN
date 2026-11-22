using Arch.Core;

using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Cameras;

/// <summary>
/// Marks the camera a director has made live, with the lens and world pose it is drawn with this frame, so the camera's own
/// transform stays as authored; the renderer draws it first. Set while playing, never saved in scenes.
/// </summary>
public struct ActiveCamera
{
    private static readonly QueryDescription Actives = new QueryDescription().WithAll<ActiveCamera>();
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>();

    public Camera Lens { get; set; }

    public Transform Pose { get; set; }

    /// <summary>The view the world is drawn from: the active camera's pose, else the first camera's own, or null without a camera.</summary>
    public static RenderCamera? Find(World world)
    {
        RenderCamera? found = null;
        world.Query(in Actives, (ref ActiveCamera active) =>
        {
            found ??= new RenderCamera(active.Lens, active.Pose);
        });

        if (found is not null)
        {
            return found;
        }

        world.Query(in Cameras, (ref Camera camera, ref Transform transform) =>
        {
            found ??= new RenderCamera(camera, transform);
        });
        return found;
    }
}
