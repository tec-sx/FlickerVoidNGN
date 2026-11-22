using System.Numerics;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Rendering.Extraction;

namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>What camera layers see of a frame: its length, the shot's pose before any layer, its target, the pointer and the requests sent.</summary>
public sealed class CameraLayerFrame
{
    public float Seconds { get; internal set; }

    public RenderCamera View { get; internal set; }

    /// <summary>Where the shot's target is aimed at, or null without one.</summary>
    public Vector3? Target { get; internal set; }

    /// <summary>The pointer from the centre of the screen, -1 to 1 with +Y up, or null when unknown.</summary>
    public Vector2? Pointer { get; internal set; }

    public IReadOnlyList<CameraShakeCommand> Shakes { get; internal set; } = [];

    public IReadOnlyList<ZoomPunchCommand> Punches { get; internal set; } = [];
}
