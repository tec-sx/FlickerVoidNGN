using System.Numerics;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// How a scene is lit and finished: the sun and ambient light, the fog, the screen-space shadows and the post-processing
/// grade. Neutral grading by default, so a mood only names what it changes.
/// </summary>
public sealed record LightingMood
{
    /// <summary>The direction the sunlight travels, in world space.</summary>
    public Vector3 SunDirection { get; set; } = Vector3.Normalize(new Vector3(-0.4f, -0.8f, -0.45f));

    /// <summary>The share of light that reaches every surface, from 0 to 1; the sun lights the rest.</summary>
    public float Ambient { get; set; } = 0.35f;

    /// <summary>How far from the camera the sun casts shadows, in world units; 0 turns them off.</summary>
    public float SunShadowDistance { get; set; } = 30;

    /// <summary>How much of the light crossing a world unit of fog it absorbs or scatters, at and below <see cref="FogHeight"/>; 0 clears the air.</summary>
    public float FogDensity { get; set; }

    /// <summary>The share of the light fog scatters rather than absorbs, per color channel: white fog glows, black fog only dims.</summary>
    public Vector3 FogColor { get; set; } = Vector3.One;

    /// <summary>
    /// How much fog scatters light onward rather than back, from -1 to 1: above 0 it glows toward the camera when the
    /// light shines from behind what it sees, as in god rays; 0 scatters evenly.
    /// </summary>
    public float FogAnisotropy { get; set; } = 0.3f;

    /// <summary>The height below which fog keeps its full density.</summary>
    public float FogHeight { get; set; }

    /// <summary>How fast fog thins above <see cref="FogHeight"/>: its density falls by e every 1 / falloff units; 0 keeps it even.</summary>
    public float FogHeightFalloff { get; set; }

    /// <summary>How strongly nearby surfaces darken the ambient and bounced light in creases and corners, from 0 to 1.</summary>
    public float AmbientOcclusion { get; set; } = 1;

    /// <summary>How far ambient occlusion looks for nearby surfaces, in world units.</summary>
    public float AmbientOcclusionRadius { get; set; } = 0.5f;

    /// <summary>How far toward the sun contact shadows look for small blockers the shadow map misses; 0 turns them off.</summary>
    public float ContactShadowLength { get; set; } = 0.3f;

    /// <summary>How much of the image is replaced by its blurred glow, from 0 to 1.</summary>
    public float Bloom { get; set; }

    /// <summary>Scales the light before grading.</summary>
    public float Exposure { get; set; } = 1;

    /// <summary>Spreads colors away from middle grey (above 1) or toward it (below 1).</summary>
    public float Contrast { get; set; } = 1;

    /// <summary>0 turns the image grey, 1 leaves it, above 1 deepens colors.</summary>
    public float Saturation { get; set; } = 1;

    /// <summary>Multiplies the graded colors.</summary>
    public Vector3 Tint { get; set; } = Vector3.One;

    /// <summary>The strength of the noise added to every pixel, as a share of full brightness.</summary>
    public float Grain { get; set; }

    /// <summary>An image file in the content folder laid over the screen at one texel per pixel and multiplied in, such as paper.</summary>
    public string? Paper { get; set; }

    /// <summary>This mood with neutral post-processing, for captures that need the light as it is.</summary>
    public LightingMood WithoutPost()
    {
        return this with { Bloom = 0, Exposure = 1, Contrast = 1, Saturation = 1, Tint = Vector3.One, Grain = 0, Paper = null };
    }
}
