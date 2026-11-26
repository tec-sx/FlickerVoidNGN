using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Sky;

/// <summary>
/// The way the sun crosses a place's sky over the day: its latitude, which way north lies in the world (degrees turned
/// clockwise seen from above, from -Z) and the sun's declination (0 at an equinox, about 23.44 at midsummer in the north).
/// </summary>
public readonly record struct SunPath(float Latitude, float North, float Declination)
{
    private const float DegreesPerHour = 15;

    /// <summary>The direction from the ground toward the sun at an hour of the day, noon at 12; below the horizon at night.</summary>
    public Vector3 TowardSun(double hour)
    {
        float hourAngle = ToRadians((float)(hour - 12) * DegreesPerHour);
        float latitude = ToRadians(Latitude);
        float declination = ToRadians(Declination);
        float east = -MathF.Cos(declination) * MathF.Sin(hourAngle);
        float north = (MathF.Cos(latitude) * MathF.Sin(declination)) - (MathF.Sin(latitude) * MathF.Cos(declination) * MathF.Cos(hourAngle));
        float up = (MathF.Sin(latitude) * MathF.Sin(declination)) + (MathF.Cos(latitude) * MathF.Cos(declination) * MathF.Cos(hourAngle));
        float turn = ToRadians(North);
        Vector3 northAxis = new(MathF.Sin(turn), 0, -MathF.Cos(turn));
        Vector3 eastAxis = new(MathF.Cos(turn), 0, MathF.Sin(turn));
        return (eastAxis * east) + (Vector3.UnitY * up) + (northAxis * north);
    }

    /// <summary>Writes the way the sun's light travels at the hour onto a mood.</summary>
    public void Apply(double hour, LightingMood mood)
    {
        mood.SunDirection = -TowardSun(hour);
    }

    private static float ToRadians(float degrees) => degrees * MathF.PI / 180;
}
