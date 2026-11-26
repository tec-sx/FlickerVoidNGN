using System.ComponentModel.DataAnnotations;
using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>One copy of the bright lights a lens flare draws: how far out along the line through the screen's middle, and its colour.</summary>
public sealed record FlareGhost
{
    /// <summary>The copy's scale about the middle; negative mirrors it through the middle, as the lens's ghosts do.</summary>
    [Range(-4, 4)]
    public float Scale { get; set; } = -1;

    /// <summary>Multiplies the copied light.</summary>
    [Color]
    public Vector3 Color { get; set; } = Vector3.One;
}
