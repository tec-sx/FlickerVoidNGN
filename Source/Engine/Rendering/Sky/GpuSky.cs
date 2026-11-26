using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Sky;

/// <summary>The sky's colors, sun, moon and stars for the sky shader; mirrors SkyData in Sky.hlsl, 144 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GpuSky(
    Vector3 Zenith,
    float HorizonSoftness,
    Vector3 Horizon,
    float SunDiscCosine,
    Vector3 Ground,
    float SunDiscIntensity,
    Vector3 TowardSun,
    float SunGlow,
    Vector3 SunColor,
    float SunGlowRadians,
    Vector3 TowardMoon,
    float MoonDiscCosine,
    Vector3 MoonColor,
    float MoonDiscIntensity,
    float MoonCorona,
    float MoonDiscRadians,
    float StarIntensity,
    float SkyTanHalfView,
    Vector3 StarPole,
    float MaskStrength,
    Vector3 BandColor,
    float NorthRadians)
{
    /// <summary>The sky of a mood, painted by a mask whose north lies that many degrees round, or plain.</summary>
    public static GpuSky From(LightingMood mood, float north)
    {
        Vector3 towardSun = -Normalized(mood.SunDirection);
        Vector3 towardMoon = -Normalized(mood.MoonDirection);
        float sunRadius = ToRadians(mood.SunDiscSize / 2);
        float moonRadius = ToRadians(mood.MoonDiscSize / 2);
        return new GpuSky(
            mood.SkyZenith,
            mood.HorizonSoftness,
            mood.SkyHorizon,
            MathF.Cos(sunRadius),
            mood.SkyGround,
            mood.SunDiscIntensity,
            towardSun,
            mood.SunGlow,
            mood.SunColor,
            ToRadians(mood.SunGlowSize),
            towardMoon,
            MathF.Cos(moonRadius),
            mood.MoonColor,
            mood.MoonDiscIntensity,
            mood.MoonCorona,
            moonRadius,
            mood.StarIntensity,
            MathF.Tan(ToRadians(mood.SkyFieldOfView / 2)),
            Normalized(mood.StarPole),
            mood.SkyMaskStrength,
            mood.SkyBandColor,
            ToRadians(north));
    }

    private static float ToRadians(float degrees) => degrees * MathF.PI / 180;

    private static Vector3 Normalized(Vector3 direction)
    {
        if (direction.LengthSquared() <= 0)
        {
            return -Vector3.UnitY;
        }

        return Vector3.Normalize(direction);
    }
}
