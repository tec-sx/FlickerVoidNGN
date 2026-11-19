using System.Numerics;

namespace FlickerVoid.Engine.Rendering;

/// <summary>Light shining in a cone along its entity's -Z, full inside the inner angle and fading out to the outer one.</summary>
public struct SpotLight
{
    public SpotLight()
    {
    }

    public Vector3 Color { get; set; } = Vector3.One;

    /// <summary>Scales the color, as a <see cref="PointLight"/>'s does.</summary>
    public float Intensity { get; set; } = 1;

    /// <summary>How far the light reaches, in world units.</summary>
    public float Range { get; set; } = 5;

    /// <summary>The angle in degrees between the cone's axis and its edge of full light.</summary>
    public float InnerConeAngle { get; set; } = 20;

    /// <summary>The angle in degrees between the cone's axis and its outer edge, where the light has faded out.</summary>
    public float OuterConeAngle { get; set; } = 30;

    /// <summary>Whether meshes block its light, through a tile of the <see cref="ShadowAtlas"/> while tiles are free.</summary>
    public bool CastsShadows { get; set; }

    /// <summary>
    /// An image file in the content folder that tints the light like a slide in a projector, filling the outer cone with the
    /// entity's +X to the right and +Y up; none lights the cone evenly.
    /// </summary>
    public string? Cookie { get; set; }
}
