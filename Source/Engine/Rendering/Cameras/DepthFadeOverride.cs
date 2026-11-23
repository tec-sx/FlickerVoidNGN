using System.ComponentModel.DataAnnotations;
using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Cameras;

/// <summary>Changes the near or far side of the <see cref="MainCamera"/>'s depth fade for one camera, such as a cutscene's shot.</summary>
public struct DepthFadeOverride
{
    public DepthFadeOverride()
    {
    }

    /// <summary>Whether this camera's near values replace the main camera's.</summary>
    public bool OverrideNear { get; set; } = true;

    public float NearStart { get; set; } = 1;

    public float NearEnd { get; set; } = 6;

    [Range(0, 1)]
    public float NearAmount { get; set; }

    /// <summary>Whether this camera's far values replace the main camera's.</summary>
    public bool OverrideFar { get; set; } = true;

    public float FarStart { get; set; } = 2;

    public float FarEnd { get; set; } = 20;

    [Range(0, 1)]
    public float FarAmount { get; set; }

    [Color]
    public Vector3 FarColor { get; set; } = new(0.82f, 0.8f, 0.76f);

    /// <summary>The main camera's fade with the sides this override names replaced.</summary>
    public readonly DepthFade Apply(in DepthFade main)
    {
        DepthFade fade = main;
        if (OverrideNear)
        {
            fade = fade with { NearStart = NearStart, NearEnd = NearEnd, NearAmount = NearAmount };
        }

        if (OverrideFar)
        {
            fade = fade with { FarStart = FarStart, FarEnd = FarEnd, FarAmount = FarAmount, FarColor = FarColor };
        }

        return fade;
    }
}
