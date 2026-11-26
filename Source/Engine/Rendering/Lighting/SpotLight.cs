using System.ComponentModel.DataAnnotations;
using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>Light shining in a cone along its entity's -Z, full inside the inner angle and fading out to the outer one.</summary>
public struct SpotLight
{
    public SpotLight()
    {
    }

    [Color]
    public Vector3 Color { get; set; } = Vector3.One;

    /// <summary>Scales the color, as a <see cref="PointLight"/>'s does.</summary>
    public float Intensity { get; set; } = 1;

    /// <summary>How far the light reaches, in world units.</summary>
    public float Range { get; set; } = 5;

    /// <summary>The angle in degrees between the cone's axis and its edge of full light.</summary>
    [Range(0, 90)]
    public float InnerConeAngle { get; set; } = 20;

    /// <summary>The angle in degrees between the cone's axis and its outer edge, where the light has faded out.</summary>
    [Range(0, 90)]
    public float OuterConeAngle { get; set; } = 30;

    /// <summary>
    /// Whether meshes block its light: through traced rays while the frame traces them and slots are free, else through a tile
    /// of the <see cref="ShadowAtlas"/> while tiles are free.
    /// </summary>
    public bool CastsShadows { get; set; }

    /// <summary>The radius of the bulb or flame, as a <see cref="PointLight"/>'s.</summary>
    public float SourceRadius { get; set; } = 0.05f;

    /// <summary>
    /// An image file in the content folder that tints the light like a slide in a projector, filling the outer cone with the
    /// entity's +X to the right and +Y up; none lights the cone evenly.
    /// </summary>
    [ContentFile(".png", ".jpg", ".jpeg", ".tga", ".bmp", ".reb")]
    public string? Cookie { get; set; }
}

