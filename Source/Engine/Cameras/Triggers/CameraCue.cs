using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Cameras.Triggers;

/// <summary>
/// Turns a trigger area into a camera request: switching to, pushing or returning from a shot with a transition, playing it, and
/// shaking the camera, as an activator enters or leaves, while its facts hold, optionally once.
/// </summary>
public struct CameraCue
{
    public CameraCue()
    {
    }

    /// <summary>The name of the shot entity; empty changes no shot (a return needs none).</summary>
    [EntityReference]
    public string Shot { get; set; } = string.Empty;

    public ShotSwitchMode Mode { get; set; }

    public ShotTransition Transition { get; set; } = ShotTransition.Cut;

    public CueMoment On { get; set; }

    /// <summary>Whether to play the shot as well, to <see cref="ToKey"/> or its end.</summary>
    public bool Play { get; set; }

    /// <summary>The key to play to; -1 plays to the end.</summary>
    public int ToKey { get; set; } = -1;

    /// <summary>Whether the shot's layers move it while it plays.</summary>
    public bool Layers { get; set; } = true;

    /// <summary>How hard to shake the camera, from 0 (not at all) to 1.</summary>
    public float ShakeStrength { get; set; }

    public float ShakeDuration { get; set; } = 0.5f;

    /// <summary>Whether it fires only the first time.</summary>
    public bool Once { get; set; }

    /// <summary>Facts that must all hold for it to fire.</summary>
    public List<FactCondition> When { get; set; } = [];
}
