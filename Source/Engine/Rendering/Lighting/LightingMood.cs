using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Moods;
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
    public const string Extension = ".fvmood";

    /// <summary>The schema the file names for text editors; the game ignores it.</summary>
    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    /// <summary>The direction the sunlight travels, in world space, of any length.</summary>
    public Vector3 SunDirection { get; set; } = Vector3.Normalize(new Vector3(-0.4f, -0.8f, -0.45f));

    /// <summary>The share of light that reaches every surface, from 0 to 1; the sun lights the rest.</summary>
    [Range(0, 1)]
    public float Ambient { get; set; } = 0.35f;

    /// <summary>
    /// How far from the camera the sun casts shadows through its shadow map, in world units; 0 turns them off. Traced shadows
    /// reach as far as the scene does.
    /// </summary>
    public float SunShadowDistance { get; set; } = 30;

    /// <summary>The sun's width in degrees, which softens traced shadows the farther they fall from their casters.</summary>
    [Range(0, 30)]
    public float SunAngle { get; set; } = 1;

    /// <summary>The color of the sun's light, linear.</summary>
    public Vector3 SunColor { get; set; } = Vector3.One;

    /// <summary>How strong the sun's light is, times its color; 1 lights as the sun always has.</summary>
    [Range(0, 100)]
    public float SunIntensity { get; set; } = 1;

    /// <summary>The direction the moonlight travels, in world space, of any length; the sun's path turns it with the day.</summary>
    public Vector3 MoonDirection { get; set; } = Vector3.Normalize(new Vector3(0.4f, 0.8f, 0.45f));

    /// <summary>The color of the full moon's light, linear.</summary>
    public Vector3 MoonColor { get; set; } = new(0.55f, 0.65f, 0.9f);

    /// <summary>How strong the full moon's light is, times its color; a moon less than full lights less.</summary>
    [Range(0, 100)]
    public float MoonIntensity { get; set; } = 0.25f;

    /// <summary>The sky's color overhead, as it looks on screen whatever the grade, linear.</summary>
    public Vector3 SkyZenith { get; set; } = new(0.0394f, 0.0872f, 0.1332f);

    /// <summary>The sky's color at the horizon, as it looks on screen, linear.</summary>
    public Vector3 SkyHorizon { get; set; } = new(0.2578f, 0.2373f, 0.1643f);

    /// <summary>The ground's color below the horizon, as it looks on screen, linear; it darkens farther down.</summary>
    public Vector3 SkyGround { get; set; } = new(0.0203f, 0.0177f, 0.0112f);

    /// <summary>How far above and below the horizon sky and ground blend, as the sine of the height; 0 is a hard line.</summary>
    [Range(0, 1)]
    public float HorizonSoftness { get; set; } = 0.02f;

    /// <summary>The sun's disc's width across the sky, in degrees.</summary>
    [Range(0, 30)]
    public float SunDiscSize { get; set; } = 2;

    /// <summary>How bright the sun's disc is, times the sun's color, as light, so it blooms.</summary>
    [Range(0, 1000)]
    public float SunDiscIntensity { get; set; } = 8;

    /// <summary>How bright the glow round the sun is, times the sun's color, as light.</summary>
    [Range(0, 100)]
    public float SunGlow { get; set; } = 0.4f;

    /// <summary>How far from the sun its glow reaches, in degrees.</summary>
    [Range(0, 180)]
    public float SunGlowSize { get; set; } = 25;

    /// <summary>The moon's disc's width across the sky, in degrees.</summary>
    [Range(0, 30)]
    public float MoonDiscSize { get; set; } = 2.5f;

    /// <summary>How bright the lit face of the moon is, times the moon's color, as light.</summary>
    [Range(0, 1000)]
    public float MoonDiscIntensity { get; set; } = 2;

    /// <summary>How bright the corona round the moon is, times its color and its lit share, as light.</summary>
    [Range(0, 100)]
    public float MoonCorona { get; set; } = 0.15f;

    /// <summary>The celestial pole the stars turn about, in world space; the sun's path points it north, as high as the latitude.</summary>
    public Vector3 StarPole { get; set; } = Vector3.UnitY;

    /// <summary>How bright the stars are, as light; they show only where the sky is dark.</summary>
    [Range(0, 100)]
    public float StarIntensity { get; set; } = 1;

    /// <summary>How much the scene's painted sky mask shapes the sky, from the plain gradient (0) to the painted one (1).</summary>
    [Range(0, 1)]
    public float SkyMaskStrength { get; set; } = 1;

    /// <summary>The color of the painted sky's band (the mask's green), as it looks on screen, linear.</summary>
    public Vector3 SkyBandColor { get; set; } = new(0.45f, 0.4f, 0.36f);

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

    /// <summary>How far from the camera along the view the fog is lit, in units; beyond it the haze takes over and the fog adds nothing.</summary>
    [Range(1, 500)]
    public float FogRange { get; set; } = 60;

    /// <summary>
    /// How fast the haze thickens with the distance beyond its start, as a share of the light it takes per world unit; 0 leaves no haze.
    /// The haze melts what lies far from the view's focus into <see cref="HazeColor"/>, cheaply, and the sky takes it at the horizon.
    /// </summary>
    public float HazeDensity { get; set; }

    /// <summary>How far behind the focus the haze begins, along the view.</summary>
    public float HazeStart { get; set; } = 2;

    /// <summary>The most the haze melts anything into its colour, from 0 to 1.</summary>
    [Range(0, 1)]
    public float HazeMax { get; set; } = 1;

    /// <summary>The height below which the haze keeps its full density.</summary>
    public float HazeHeight { get; set; }

    /// <summary>How fast the haze thins above <see cref="HazeHeight"/>: its density falls by e every 1 / falloff units; 0 keeps it even.</summary>
    public float HazeHeightFalloff { get; set; }

    /// <summary>The haze's color as it looks on screen, linear, where it does not follow the sky (see <see cref="HazeFromSky"/>).</summary>
    [Color]
    public Vector3 HazeColor { get; set; } = new(0.82f, 0.8f, 0.76f);

    /// <summary>How much the haze takes the sky's horizon color in place of <see cref="HazeColor"/>, from 0 to 1.</summary>
    [Range(0, 1)]
    public float HazeFromSky { get; set; } = 1;

    /// <summary>How strongly nearby surfaces darken the ambient and bounced light in creases and corners, from 0 to 1.</summary>
    [Range(0, 1)]
    public float AmbientOcclusion { get; set; } = 1;

    /// <summary>How far ambient occlusion looks for nearby surfaces, in world units: on screen, or along traced rays.</summary>
    public float AmbientOcclusionRadius { get; set; } = 0.5f;

    /// <summary>
    /// How far toward the sun contact shadows look for small blockers the shadow map misses; 0 turns them off. Traced sun shadows
    /// need none.
    /// </summary>
    public float ContactShadowLength { get; set; } = 0.3f;

    /// <summary>How much of the image is replaced by its blurred glow, from 0 to 1.</summary>
    [Range(0, 1)]
    public float Bloom { get; set; }

    /// <summary>How much of each smaller bloom level the upsample keeps: 0 leaves only the tight glow, 1 the widest.</summary>
    [Range(0, 1)]
    public float BloomRadius { get; set; } = 0.85f;

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

    /// <summary>The canvas everything is painted on, a <c>.fvcanvas</c> file; none paints on nothing.</summary>
    [ContentFile(".fvcanvas")]
    public string? Canvas { get; set; }

    /// <summary>
    /// The field of view, in degrees from top to bottom, an orthographic camera sees the sky through, looking level along its
    /// heading, so the sun, moon and stars stand at real directions and move as it turns.
    /// </summary>
    [Range(1, 170)]
    public float SkyFieldOfView { get; set; } = 60;

    /// <summary>How far in front of the focus things start to fall into shadow and burn.</summary>
    public float DepthNearStart { get; set; } = 1;

    /// <summary>How far in front of the focus things take the whole of <see cref="DepthNearAmount"/> and <see cref="DepthNearBurn"/>.</summary>
    public float DepthNearEnd { get; set; } = 6;

    /// <summary>
    /// How much of its direct light (the sun's and the lamps') a near object loses, from 0 (none) to 1 (all, leaving the ambient
    /// light), as if a shadow fell on it.
    /// </summary>
    [Range(0, 1)]
    public float DepthNearAmount { get; set; }

    /// <summary>How much a near object is burned, as a photographer burns a print in: mid-tones sink and colours deepen, highlights hold; from 0 to 1.</summary>
    [Range(0, 1)]
    public float DepthNearBurn { get; set; }

    /// <summary>How far behind the focus things start to lose their colour.</summary>
    public float DepthFarStart { get; set; } = 2;

    /// <summary>How far behind the focus things lose the whole of <see cref="DepthFarAmount"/> of their colour.</summary>
    public float DepthFarEnd { get; set; } = 20;

    /// <summary>How much of their colour the farthest things lose, from 0 (none) to 1 (all, leaving their luminance).</summary>
    [Range(0, 1)]
    public float DepthFarAmount { get; set; }

    /// <summary>
    /// The mood <paramref name="amount"/> of the way from <paramref name="from"/> to <paramref name="to"/>: numbers and
    /// colors blend, the sun turns between its directions, and the paper and tonemapper swap halfway.
    /// </summary>
    public static LightingMood Lerp(LightingMood from, LightingMood to, float amount)
    {
        LightingMood blended = new();
        IReadOnlyList<MoodValue> values = MoodValues.All;
        for (int index = 0; index < values.Count; index++)
        {
            values[index].Blend(from, to, amount, blended);
        }

        return blended;
    }

    /// <summary>The depth tone its depth values spell.</summary>
    public DepthFade ToDepthFade()
    {
        return new DepthFade(DepthNearStart, DepthNearEnd, DepthNearAmount, DepthNearBurn, DepthFarStart, DepthFarEnd, DepthFarAmount);
    }

    /// <summary>This mood with neutral post-processing, for captures that need the light as it is.</summary>
    public LightingMood WithoutPost()
    {
        return this with
        {
            Bloom = 0,
            Exposure = 1,
            Contrast = 1,
            Saturation = 1,
            Tint = Vector3.One,
            Tonemapper = Tonemapper.None,
            Canvas = null,
            DepthNearAmount = 0,
            DepthNearBurn = 0,
            DepthFarAmount = 0,
        };
    }
}

