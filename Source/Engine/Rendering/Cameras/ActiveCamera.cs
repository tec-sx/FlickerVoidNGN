using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Cameras;

/// <summary>
/// Marks the camera a director has made live, with the lens and world pose it is drawn with this frame, so the camera's own
/// transform stays as authored, and the transition it lays over the frame; the renderer draws it first. Set while playing, never
/// saved in scenes.
/// </summary>
public struct ActiveCamera
{
    private static readonly QueryDescription Actives = new QueryDescription().WithAll<ActiveCamera>();
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>();

    public Camera Lens { get; set; }

    public Transform Pose { get; set; }

    /// <summary>How far the world is faded to <see cref="FadeColor"/>, from 0 to 1.</summary>
    public float Fade { get; set; }

    /// <summary>The colour of the fade, in display (sRGB) values.</summary>
    public Vector3 FadeColor { get; set; }

    /// <summary>How much of the previous shot's live view still shows over this one, from 0 to 1.</summary>
    public float Dissolve { get; set; }

    public Camera DissolveLens { get; set; }

    public Transform DissolvePose { get; set; }

    /// <summary>The world point the depth fade is measured from, such as the shot's target; null measures from the camera.</summary>
    public Vector3? Focus { get; set; }

    /// <summary>
    /// How many times the director has cut to a new view rather than moved there, so what the renderer keeps from earlier frames
    /// (an upscaler's history, traced shadows) starts afresh when it changes.
    /// </summary>
    public uint Cuts { get; set; }

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
