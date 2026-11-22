using System.Numerics;

using FlickerVoid.Engine.Cameras.Shots;

namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>How a switch to another shot looks; every switch request carries one.</summary>
public struct ShotTransition
{
    public ShotTransition()
    {
    }

    public static ShotTransition Cut => new();

    public TransitionKind Kind { get; set; }

    /// <summary>Seconds a cross-fade or blend takes, or a fade takes to go out.</summary>
    public float Duration { get; set; }

    /// <summary>Seconds a fade takes to come back in after the cut.</summary>
    public float InDuration { get; set; }

    /// <summary>The colour a fade goes to, in display values; black by default.</summary>
    public Vector3 Color { get; set; }

    /// <summary>How a blend starts and ends.</summary>
    public ShotEase Ease { get; set; } = ShotEase.EaseInOut;
}
