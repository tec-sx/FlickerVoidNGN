using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>Light shining in every direction from its entity's position, fading out to nothing at its range.</summary>
public struct PointLight
{
    public PointLight()
    {
    }

    [Color]
    public Vector3 Color { get; set; } = Vector3.One;

    /// <summary>Scales the color; a light one unit away from a surface facing it lights it by Intensity / 2.</summary>
    public float Intensity { get; set; } = 1;

    /// <summary>How far the light reaches, in world units.</summary>
    public float Range { get; set; } = 5;

    /// <summary>Whether meshes block its light, through six tiles of the <see cref="ShadowAtlas"/> while tiles are free.</summary>
    public bool CastsShadows { get; set; }
}

