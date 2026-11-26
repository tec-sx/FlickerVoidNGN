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
            FogDensity = 0.2f,
            FogColor = new Vector3(0.9f, 0.8f, 0.7f),
            FogAnisotropy = -0.2f,
            FogHeight = 3,
            FogHeightFalloff = 0.4f,
            AmbientOcclusion = 0.5f,
            AmbientOcclusionRadius = 1.5f,
            ContactShadowLength = 0.1f,
            Bloom = 0.2f,
            Exposure = 1.5f,
            Contrast = 1.2f,
            Saturation = 0.8f,
            Tint = new Vector3(1, 0.9f, 0.8f),
            Tonemapper = Tonemapper.Aces,
            WhitePoint = 8,
            Canvas = "Paper.fvcanvas",
            CanvasShading = 0.25f,
            DepthNearStart = 0.5f,
            DepthNearEnd = 3,
            DepthNearAmount = 0.4f,
            DepthFarStart = 4,
            DepthFarEnd = 12,
            DepthFarAmount = 0.3f,
            DepthFarColor = new Vector3(0.7f, 0.6f, 0.5f),
        };
    }
}
