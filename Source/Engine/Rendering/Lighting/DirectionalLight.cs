using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// The one light of a mood that shines from far away and casts the sun's shadows: the sun while it is on or above the horizon,
/// else the moon, as bright as its lit share and fading out as it sets too. Key both intensities low at the swap and shadows
/// never jump.
/// </summary>
public readonly record struct DirectionalLight(Vector3 Direction, Vector3 Light)
{
    // The sine of the moon's height over the horizon where its light has faded in fully, and where it has gone out.
    private const float FullHeight = 0.05f;
    private const float OutHeight = -0.02f;

    public static DirectionalLight Of(LightingMood mood)
    {
        if (Height(mood.SunDirection) >= 0)
        {
            Vector3 sunLight = mood.SunColor * mood.SunIntensity;
            return new DirectionalLight(mood.SunDirection, sunLight);
        }

        float fade = Math.Clamp((Height(mood.MoonDirection) - OutHeight) / (FullHeight - OutHeight), 0, 1);
        fade = fade * fade * (3 - (2 * fade));
        float strength = mood.MoonIntensity * Lit(mood) * fade;
        return new DirectionalLight(mood.MoonDirection, mood.MoonColor * strength);
    }

    /// <summary>The share of the moon's face the sun lights, from 0 when new, beside the sun, to 1 when full, opposite it.</summary>
    public static float Lit(LightingMood mood)
    {
        Vector3 sun = Normalized(mood.SunDirection);
        Vector3 moon = Normalized(mood.MoonDirection);
        return (1 - Vector3.Dot(sun, moon)) / 2;
    }

    /// <summary>The sine of the height over the horizon of the body a light travelling this way comes from.</summary>
    private static float Height(Vector3 direction) => -Normalized(direction).Y;

    private static Vector3 Normalized(Vector3 direction)
    {
        if (direction.LengthSquared() <= 0)
        {
            return -Vector3.UnitY;
        }

        return Vector3.Normalize(direction);
    }
}
