using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// How a scene is lit and finished: the sun and ambient light, the fog, the screen-space shadows and the post-processing
/// grade. Neutral grading by default, so a mood only names what it changes.
/// </summary>
public sealed record LightingMood
{
    /// <summary>The schema the file names for text editors; the game ignores it.</summary>
    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    /// <summary>The direction the sunlight travels, in world space, of any length.</summary>
    public Vector3 SunDirection { get; set; } = Vector3.Normalize(new Vector3(-0.4f, -0.8f, -0.45f));

    /// <summary>The share of light that reaches every surface, from 0 to 1; the sun lights the rest.</summary>
    [Range(0, 1)]
    public float Ambient { get; set; } = 0.35f;

    /// <summary>How far from the camera the sun casts shadows, in world units; 0 turns them off.</summary>
    public float SunShadowDistance { get; set; } = 30;

    /// <summary>How much of the light crossing a world unit of fog it absorbs or scatters, at and below <see cref="FogHeight"/>; 0 clears the air.</summary>
    public float FogDensity { get; set; }

    /// <summary>The share of the light fog scatters rather than absorbs, per color channel: white fog glows, black fog only dims.</summary>
    [Color]
    public Vector3 FogColor { get; set; } = Vector3.One;

    /// <summary>
    /// How much fog scatters light onward rather than back, from -1 to 1: above 0 it glows toward the camera when the
    /// light shines from behind what it sees, as in god rays; 0 scatters evenly.
    /// </summary>
    [Range(-1, 1)]
    public float FogAnisotropy { get; set; } = 0.3f;

    /// <summary>The height below which fog keeps its full density.</summary>
    public float FogHeight { get; set; }

    /// <summary>How fast fog thins above <see cref="FogHeight"/>: its density falls by e every 1 / falloff units; 0 keeps it even.</summary>
    public float FogHeightFalloff { get; set; }

    /// <summary>How strongly nearby surfaces darken the ambient and bounced light in creases and corners, from 0 to 1.</summary>
    [Range(0, 1)]
    public float AmbientOcclusion { get; set; } = 1;

    /// <summary>How far ambient occlusion looks for nearby surfaces, in world units.</summary>
    public float AmbientOcclusionRadius { get; set; } = 0.5f;

    /// <summary>How far toward the sun contact shadows look for small blockers the shadow map misses; 0 turns them off.</summary>
    public float ContactShadowLength { get; set; } = 0.3f;

    /// <summary>How much of the image is replaced by its blurred glow, from 0 to 1.</summary>
    [Range(0, 1)]
    public float Bloom { get; set; }

    /// <summary>Scales the light before grading.</summary>
    [Range(0, 8)]
    public float Exposure { get; set; } = 1;

    /// <summary>Spreads colors away from middle grey (above 1) or toward it (below 1).</summary>
    [Range(0, 3)]
    public float Contrast { get; set; } = 1;

    /// <summary>0 turns the image grey, 1 leaves it, above 1 deepens colors.</summary>
    [Range(0, 3)]
    public float Saturation { get; set; } = 1;

    /// <summary>Multiplies the graded colors.</summary>
    [Color]
    public Vector3 Tint { get; set; } = Vector3.One;

    /// <summary>The curve that brings the graded light into the screen's range; Uncharted 2 by default.</summary>
    public Tonemapper Tonemapper { get; set; } = Tonemapper.Uncharted2;

    /// <summary>The graded light the Uncharted 2 curve brings to full white; lower whitens the image sooner.</summary>
    [Range(1, 20)]
    public float WhitePoint { get; set; } = 11.2f;

    /// <summary>The strength of the noise added to every pixel, as a share of full brightness.</summary>
    [Range(0, 0.5)]
    public float Grain { get; set; }

    /// <summary>
    /// The paper everything is painted on: an image file in the content folder laid under the screen at one texel per pixel,
    /// unlit, showing where nothing is painted and through paint as thin as its alpha says.
    /// </summary>
    [ContentFile(".png", ".jpg", ".jpeg", ".tga", ".bmp")]
    public string? Paper { get; set; }

    /// <summary>How much the paper's grain decides where thin paint lies: it pools in the dark valleys and leaves the light peaks bare.</summary>
    [Range(0, 1)]
    public float PaperGrain { get; set; } = 0.3f;

    /// <summary>
    /// The mood <paramref name="amount"/> of the way from <paramref name="from"/> to <paramref name="to"/>: numbers and
    /// colors blend, the sun turns between its directions, and the paper and tonemapper swap halfway.
    /// </summary>
    public static LightingMood Lerp(LightingMood from, LightingMood to, float amount)
    {
        Vector3 sunDirection = Vector3.Lerp(from.SunDirection, to.SunDirection, amount);
        if (sunDirection.LengthSquared() > 1e-12f)
        {
            sunDirection = Vector3.Normalize(sunDirection);
        }
        else
        {
            sunDirection = to.SunDirection;
        }

        string? paper = from.Paper;
        Tonemapper tonemapper = from.Tonemapper;
        if (amount >= 0.5f)
        {
            paper = to.Paper;
            tonemapper = to.Tonemapper;
        }

        return new LightingMood
        {
            SunDirection = sunDirection,
            Ambient = float.Lerp(from.Ambient, to.Ambient, amount),
            SunShadowDistance = float.Lerp(from.SunShadowDistance, to.SunShadowDistance, amount),
            FogDensity = float.Lerp(from.FogDensity, to.FogDensity, amount),
            FogColor = Vector3.Lerp(from.FogColor, to.FogColor, amount),
            FogAnisotropy = float.Lerp(from.FogAnisotropy, to.FogAnisotropy, amount),
            FogHeight = float.Lerp(from.FogHeight, to.FogHeight, amount),
            FogHeightFalloff = float.Lerp(from.FogHeightFalloff, to.FogHeightFalloff, amount),
            AmbientOcclusion = float.Lerp(from.AmbientOcclusion, to.AmbientOcclusion, amount),
            AmbientOcclusionRadius = float.Lerp(from.AmbientOcclusionRadius, to.AmbientOcclusionRadius, amount),
            ContactShadowLength = float.Lerp(from.ContactShadowLength, to.ContactShadowLength, amount),
            Bloom = float.Lerp(from.Bloom, to.Bloom, amount),
            Exposure = float.Lerp(from.Exposure, to.Exposure, amount),
            Contrast = float.Lerp(from.Contrast, to.Contrast, amount),
            Saturation = float.Lerp(from.Saturation, to.Saturation, amount),
            Tint = Vector3.Lerp(from.Tint, to.Tint, amount),
            Tonemapper = tonemapper,
            WhitePoint = float.Lerp(from.WhitePoint, to.WhitePoint, amount),
            Grain = float.Lerp(from.Grain, to.Grain, amount),
            Paper = paper,
            PaperGrain = float.Lerp(from.PaperGrain, to.PaperGrain, amount),
        };
    }

    /// <summary>This mood with neutral post-processing, for captures that need the light as it is.</summary>
    public LightingMood WithoutPost()
    {
        return this with { Bloom = 0, Exposure = 1, Contrast = 1, Saturation = 1, Tint = Vector3.One, Tonemapper = Tonemapper.None, Grain = 0, Paper = null };
    }
}

