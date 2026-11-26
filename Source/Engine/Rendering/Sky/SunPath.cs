using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Sky;

/// <summary>
/// The way the sun crosses a place's sky over the day: its latitude, which way north lies in the world (degrees turned
/// clockwise seen from above, from -Z) and the sun's declination (0 at an equinox, about 23.44 at midsummer in the north).
/// </summary>
public readonly record struct SunPath(float Latitude, float North, float Declination)
{
    /// <summary>The days from one new moon to the next.</summary>
    public const float MoonMonthDays = 29.5f;

    private const float DegreesPerHour = 15;
    private const float DayHours = 24;

    /// <summary>The celestial pole the sky turns about: north, as high over the horizon as the latitude (south below the equator).</summary>
    public Vector3 Pole
    {
        get
        {
            float latitude = ToRadians(Latitude);
            float turn = ToRadians(North);
            Vector3 northAxis = new(MathF.Sin(turn), 0, -MathF.Cos(turn));
            return (northAxis * MathF.Cos(latitude)) + (Vector3.UnitY * MathF.Sin(latitude));
        }
    }

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

    /// <summary>
    /// The direction from the ground toward the moon on a day at an hour. It runs the sun's path behind the sun by its age (the
    /// day over <see cref="MoonMonthDays"/>): beside the sun when new, opposite it when full, rising later each day.
    /// </summary>
    public Vector3 TowardMoon(int day, double hour)
    {
        double age = day / (double)MoonMonthDays;
        age -= Math.Floor(age);
        double behind = age * DayHours;
        return TowardSun(hour - behind);
    }

    /// <summary>Writes the way the sun's and the moon's light travel on the day at the hour, and the stars' pole, onto a mood.</summary>
    public void Apply(int day, double hour, LightingMood mood)
    {
        mood.SunDirection = -TowardSun(hour);
        mood.MoonDirection = -TowardMoon(day, hour);
        mood.StarPole = Pole;
    }

    private static float ToRadians(float degrees) => degrees * MathF.PI / 180;
}
