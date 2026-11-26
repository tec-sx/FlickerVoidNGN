using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Tests.Moods;

/// <summary>Builds moods to read the table of mood values against.</summary>
internal sealed class MoodValuesTestsBuilder
{
    /// <summary>A mood whose every value differs from a new mood's.</summary>
    public LightingMood BuildChanged()
    {
        Vector3 sun = new(0.3f, -0.5f, 0.8f);
        return new LightingMood
        {
            SunDirection = Vector3.Normalize(sun),
            Ambient = 0.6f,
            SunShadowDistance = 12,
            SunAngle = 3,
            SunColor = new Vector3(1, 0.8f, 0.6f),
            SunIntensity = 2,
            MoonDirection = Vector3.Normalize(new Vector3(-0.3f, 0.5f, -0.8f)),
            MoonColor = new Vector3(0.4f, 0.5f, 0.9f),
            MoonIntensity = 0.6f,
            SkyZenith = new Vector3(0.1f, 0.2f, 0.5f),
            SkyHorizon = new Vector3(0.5f, 0.4f, 0.3f),
            SkyGround = new Vector3(0.05f, 0.04f, 0.03f),
            HorizonSoftness = 0.2f,
            SunDiscSize = 4,
            SunDiscIntensity = 20,
            SunGlow = 1,
            SunGlowSize = 40,
            MoonDiscSize = 5,
            MoonDiscIntensity = 3,
            MoonCorona = 0.5f,
            StarPole = Vector3.Normalize(new Vector3(0, 0.5f, -1)),
            StarIntensity = 2,
            SkyMaskStrength = 0.5f,
            SkyBandColor = new Vector3(0.8f, 0.3f, 0.2f),
            FogDensity = 0.2f,
            FogColor = new Vector3(0.9f, 0.8f, 0.7f),
            FogAnisotropy = -0.2f,
            FogHeight = 3,
            FogHeightFalloff = 0.4f,
            FogRange = 30,
            HazeDensity = 0.07f,
            HazeStart = 3,
            HazeMax = 0.6f,
            HazeHeight = 1,
            HazeHeightFalloff = 0.3f,
            HazeColor = new Vector3(0.7f, 0.6f, 0.5f),
            HazeFromSky = 0.5f,
            AmbientOcclusion = 0.5f,
            AmbientOcclusionRadius = 1.5f,
            Reflection = "Sky.png",
            ReflectionIntensity = 0.5f,
            ContactShadowLength = 0.1f,
            Bloom = 0.2f,
            BloomRadius = 0.4f,
            Dirt = "Smudge.png",
            DirtAmount = 0.3f,
            VignetteIntensity = 0.4f,
            VignetteRadius = 0.5f,
            VignetteSoftness = 0.3f,
            VignetteRoundness = 0.6f,
            VignetteColor = new Vector3(0.1f, 0.2f, 0.3f),
            Flare = "Lens.fvflare",
            FlareIntensity = 0.5f,
            FlareThreshold = 3,
            FlareThresholdRange = 2,
            Exposure = 1.5f,
            Contrast = 1.2f,
            Saturation = 0.8f,
            Tint = new Vector3(1, 0.9f, 0.8f),
            Tonemapper = Tonemapper.Aces,
            WhitePoint = 8,
            Canvas = "Paper.fvcanvas",
            SkyFieldOfView = 40,
            DepthNearStart = 0.5f,
            DepthNearEnd = 3,
            DepthNearAmount = 0.4f,
            DepthNearBurn = 0.2f,
            DepthFarStart = 4,
            DepthFarEnd = 12,
            DepthFarAmount = 0.3f,
        };
    }
}
