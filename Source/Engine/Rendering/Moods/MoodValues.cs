using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>
/// Every value of a <see cref="LightingMood"/>, listed once in the mood's own order, for whatever blends, overrides, keys
/// or edits moods value by value.
/// </summary>
public static class MoodValues
{
    private const MoodOwner Environment = MoodOwner.Environment;
    private const MoodOwner Lens = MoodOwner.Lens;

    private static readonly MoodValue[] Values =
    [
        new MoodDirection("sunDirection", MoodGroup.Sun, Environment, mood => mood.SunDirection, (mood, value) => mood.SunDirection = value),
        new MoodNumber("ambient", MoodGroup.Sky, Environment, mood => mood.Ambient, (mood, value) => mood.Ambient = value),
        new MoodNumber("sunShadowDistance", MoodGroup.Sun, Environment, mood => mood.SunShadowDistance, (mood, value) => mood.SunShadowDistance = value),
        new MoodNumber("sunAngle", MoodGroup.Sun, Environment, mood => mood.SunAngle, (mood, value) => mood.SunAngle = value),
        new MoodColor("sunColor", MoodGroup.Sun, Environment, mood => mood.SunColor, (mood, value) => mood.SunColor = value),
        new MoodNumber("sunIntensity", MoodGroup.Sun, Environment, mood => mood.SunIntensity, (mood, value) => mood.SunIntensity = value),
        new MoodDirection("moonDirection", MoodGroup.Sun, Environment, mood => mood.MoonDirection, (mood, value) => mood.MoonDirection = value),
        new MoodColor("moonColor", MoodGroup.Sun, Environment, mood => mood.MoonColor, (mood, value) => mood.MoonColor = value),
        new MoodNumber("moonIntensity", MoodGroup.Sun, Environment, mood => mood.MoonIntensity, (mood, value) => mood.MoonIntensity = value),
        new MoodColor("skyZenith", MoodGroup.Sky, Environment, mood => mood.SkyZenith, (mood, value) => mood.SkyZenith = value),
        new MoodColor("skyHorizon", MoodGroup.Sky, Environment, mood => mood.SkyHorizon, (mood, value) => mood.SkyHorizon = value),
        new MoodColor("skyGround", MoodGroup.Sky, Environment, mood => mood.SkyGround, (mood, value) => mood.SkyGround = value),
        new MoodNumber("horizonSoftness", MoodGroup.Sky, Environment, mood => mood.HorizonSoftness, (mood, value) => mood.HorizonSoftness = value),
        new MoodNumber("sunDiscSize", MoodGroup.Sky, Environment, mood => mood.SunDiscSize, (mood, value) => mood.SunDiscSize = value),
        new MoodNumber("sunDiscIntensity", MoodGroup.Sky, Environment, mood => mood.SunDiscIntensity, (mood, value) => mood.SunDiscIntensity = value),
        new MoodNumber("sunGlow", MoodGroup.Sky, Environment, mood => mood.SunGlow, (mood, value) => mood.SunGlow = value),
        new MoodNumber("sunGlowSize", MoodGroup.Sky, Environment, mood => mood.SunGlowSize, (mood, value) => mood.SunGlowSize = value),
        new MoodNumber("moonDiscSize", MoodGroup.Sky, Environment, mood => mood.MoonDiscSize, (mood, value) => mood.MoonDiscSize = value),
        new MoodNumber("moonDiscIntensity", MoodGroup.Sky, Environment, mood => mood.MoonDiscIntensity, (mood, value) => mood.MoonDiscIntensity = value),
        new MoodNumber("moonCorona", MoodGroup.Sky, Environment, mood => mood.MoonCorona, (mood, value) => mood.MoonCorona = value),
        new MoodDirection("starPole", MoodGroup.Sky, Environment, mood => mood.StarPole, (mood, value) => mood.StarPole = value),
        new MoodNumber("starIntensity", MoodGroup.Sky, Environment, mood => mood.StarIntensity, (mood, value) => mood.StarIntensity = value),
        new MoodNumber("skyMaskStrength", MoodGroup.Sky, Environment, mood => mood.SkyMaskStrength, (mood, value) => mood.SkyMaskStrength = value),
        new MoodColor("skyBandColor", MoodGroup.Sky, Environment, mood => mood.SkyBandColor, (mood, value) => mood.SkyBandColor = value),
        new MoodNumber("fogDensity", MoodGroup.Fog, Environment, mood => mood.FogDensity, (mood, value) => mood.FogDensity = value),
        new MoodColor("fogColor", MoodGroup.Fog, Environment, mood => mood.FogColor, (mood, value) => mood.FogColor = value),
        new MoodNumber("fogAnisotropy", MoodGroup.Fog, Environment, mood => mood.FogAnisotropy, (mood, value) => mood.FogAnisotropy = value),
        new MoodNumber("fogHeight", MoodGroup.Fog, Environment, mood => mood.FogHeight, (mood, value) => mood.FogHeight = value),
        new MoodNumber("fogHeightFalloff", MoodGroup.Fog, Environment, mood => mood.FogHeightFalloff, (mood, value) => mood.FogHeightFalloff = value),
        new MoodNumber("fogRange", MoodGroup.Fog, Environment, mood => mood.FogRange, (mood, value) => mood.FogRange = value),
        new MoodNumber("fogNoise", MoodGroup.Fog, Environment, mood => mood.FogNoise, (mood, value) => mood.FogNoise = value),
        new MoodNumber("fogNoiseScale", MoodGroup.Fog, Environment, mood => mood.FogNoiseScale, (mood, value) => mood.FogNoiseScale = value),
        new MoodDirection("fogWind", MoodGroup.Fog, Environment, mood => mood.FogWind, (mood, value) => mood.FogWind = value),
        new MoodNumber("fogWindSpeed", MoodGroup.Fog, Environment, mood => mood.FogWindSpeed, (mood, value) => mood.FogWindSpeed = value),
        new MoodNumber("beamIntensity", MoodGroup.Fog, Environment, mood => mood.BeamIntensity, (mood, value) => mood.BeamIntensity = value),
        new MoodNumber("beamWidth", MoodGroup.Fog, Environment, mood => mood.BeamWidth, (mood, value) => mood.BeamWidth = value),
        new MoodNumber("beamReach", MoodGroup.Fog, Environment, mood => mood.BeamReach, (mood, value) => mood.BeamReach = value),
        new MoodColor("beamColor", MoodGroup.Fog, Environment, mood => mood.BeamColor, (mood, value) => mood.BeamColor = value),
        new MoodNumber("rain", MoodGroup.Weather, Environment, mood => mood.Rain, (mood, value) => mood.Rain = value),
        new MoodNumber("dust", MoodGroup.Weather, Environment, mood => mood.Dust, (mood, value) => mood.Dust = value),
        new MoodNumber("hazeDensity", MoodGroup.Fog, Environment, mood => mood.HazeDensity, (mood, value) => mood.HazeDensity = value),
        new MoodNumber("hazeStart", MoodGroup.Fog, Environment, mood => mood.HazeStart, (mood, value) => mood.HazeStart = value),
        new MoodNumber("hazeMax", MoodGroup.Fog, Environment, mood => mood.HazeMax, (mood, value) => mood.HazeMax = value),
        new MoodNumber("hazeHeight", MoodGroup.Fog, Environment, mood => mood.HazeHeight, (mood, value) => mood.HazeHeight = value),
        new MoodNumber("hazeHeightFalloff", MoodGroup.Fog, Environment, mood => mood.HazeHeightFalloff, (mood, value) => mood.HazeHeightFalloff = value),
        new MoodColor("hazeColor", MoodGroup.Fog, Environment, mood => mood.HazeColor, (mood, value) => mood.HazeColor = value),
        new MoodNumber("hazeFromSky", MoodGroup.Fog, Environment, mood => mood.HazeFromSky, (mood, value) => mood.HazeFromSky = value),
        new MoodNumber("ambientOcclusion", MoodGroup.Sky, Environment, mood => mood.AmbientOcclusion, (mood, value) => mood.AmbientOcclusion = value),
        new MoodNumber("ambientOcclusionRadius", MoodGroup.Sky, Environment, mood => mood.AmbientOcclusionRadius, (mood, value) => mood.AmbientOcclusionRadius = value),
        new MoodChoice<string?>("reflection", MoodGroup.Sky, Environment, mood => mood.Reflection, (mood, value) => mood.Reflection = value),
        new MoodNumber("reflectionIntensity", MoodGroup.Sky, Environment, mood => mood.ReflectionIntensity, (mood, value) => mood.ReflectionIntensity = value),
        new MoodNumber("contactShadowLength", MoodGroup.Sun, Environment, mood => mood.ContactShadowLength, (mood, value) => mood.ContactShadowLength = value),
        new MoodNumber("bloom", MoodGroup.Bloom, Lens, mood => mood.Bloom, (mood, value) => mood.Bloom = value),
        new MoodNumber("bloomRadius", MoodGroup.Bloom, Lens, mood => mood.BloomRadius, (mood, value) => mood.BloomRadius = value),
        new MoodChoice<string?>("dirt", MoodGroup.Bloom, Lens, mood => mood.Dirt, (mood, value) => mood.Dirt = value),
        new MoodNumber("dirtAmount", MoodGroup.Bloom, Lens, mood => mood.DirtAmount, (mood, value) => mood.DirtAmount = value),
        new MoodNumber("vignetteIntensity", MoodGroup.Bloom, Lens, mood => mood.VignetteIntensity, (mood, value) => mood.VignetteIntensity = value),
        new MoodNumber("vignetteRadius", MoodGroup.Bloom, Lens, mood => mood.VignetteRadius, (mood, value) => mood.VignetteRadius = value),
        new MoodNumber("vignetteSoftness", MoodGroup.Bloom, Lens, mood => mood.VignetteSoftness, (mood, value) => mood.VignetteSoftness = value),
        new MoodNumber("vignetteRoundness", MoodGroup.Bloom, Lens, mood => mood.VignetteRoundness, (mood, value) => mood.VignetteRoundness = value),
        new MoodColor("vignetteColor", MoodGroup.Bloom, Lens, mood => mood.VignetteColor, (mood, value) => mood.VignetteColor = value),
        new MoodChoice<string?>("flare", MoodGroup.Bloom, Lens, mood => mood.Flare, (mood, value) => mood.Flare = value),
        new MoodNumber("flareIntensity", MoodGroup.Bloom, Lens, mood => mood.FlareIntensity, (mood, value) => mood.FlareIntensity = value),
        new MoodNumber("flareThreshold", MoodGroup.Bloom, Lens, mood => mood.FlareThreshold, (mood, value) => mood.FlareThreshold = value),
        new MoodNumber("flareThresholdRange", MoodGroup.Bloom, Lens, mood => mood.FlareThresholdRange, (mood, value) => mood.FlareThresholdRange = value),
        new MoodNumber("exposure", MoodGroup.Grade, Environment, mood => mood.Exposure, (mood, value) => mood.Exposure = value),
        new MoodNumber("contrast", MoodGroup.Grade, Environment, mood => mood.Contrast, (mood, value) => mood.Contrast = value),
        new MoodNumber("saturation", MoodGroup.Grade, Environment, mood => mood.Saturation, (mood, value) => mood.Saturation = value),
        new MoodColor("tint", MoodGroup.Grade, Environment, mood => mood.Tint, (mood, value) => mood.Tint = value),
        new MoodChoice<Tonemapper>("tonemapper", MoodGroup.Tonemapper, Lens, mood => mood.Tonemapper, (mood, value) => mood.Tonemapper = value),
        new MoodNumber("whitePoint", MoodGroup.Tonemapper, Lens, mood => mood.WhitePoint, (mood, value) => mood.WhitePoint = value),
        new MoodChoice<string?>("canvas", MoodGroup.Canvas, Lens, mood => mood.Canvas, (mood, value) => mood.Canvas = value),
        new MoodNumber("skyFieldOfView", MoodGroup.Sky, Lens, mood => mood.SkyFieldOfView, (mood, value) => mood.SkyFieldOfView = value),
        new MoodNumber("depthNearStart", MoodGroup.Depth, Lens, mood => mood.DepthNearStart, (mood, value) => mood.DepthNearStart = value),
        new MoodNumber("depthNearEnd", MoodGroup.Depth, Lens, mood => mood.DepthNearEnd, (mood, value) => mood.DepthNearEnd = value),
        new MoodNumber("depthNearAmount", MoodGroup.Depth, Lens, mood => mood.DepthNearAmount, (mood, value) => mood.DepthNearAmount = value),
        new MoodNumber("depthNearBurn", MoodGroup.Depth, Lens, mood => mood.DepthNearBurn, (mood, value) => mood.DepthNearBurn = value),
        new MoodNumber("depthFarStart", MoodGroup.Depth, Lens, mood => mood.DepthFarStart, (mood, value) => mood.DepthFarStart = value),
        new MoodNumber("depthFarEnd", MoodGroup.Depth, Lens, mood => mood.DepthFarEnd, (mood, value) => mood.DepthFarEnd = value),
        new MoodNumber("depthFarAmount", MoodGroup.Depth, Lens, mood => mood.DepthFarAmount, (mood, value) => mood.DepthFarAmount = value),
    ];

    private static readonly Dictionary<string, MoodValue> ByName = Values.ToDictionary(value => value.Name);

    public static IReadOnlyList<MoodValue> All => Values;

    /// <summary>The value files spell with a name, or none.</summary>
    public static MoodValue? Find(string name) => ByName.GetValueOrDefault(name);
}
