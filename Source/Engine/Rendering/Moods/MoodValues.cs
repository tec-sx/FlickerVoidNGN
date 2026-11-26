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
        new MoodNumber("fogDensity", MoodGroup.Fog, Environment, mood => mood.FogDensity, (mood, value) => mood.FogDensity = value),
        new MoodColor("fogColor", MoodGroup.Fog, Environment, mood => mood.FogColor, (mood, value) => mood.FogColor = value),
        new MoodNumber("fogAnisotropy", MoodGroup.Fog, Environment, mood => mood.FogAnisotropy, (mood, value) => mood.FogAnisotropy = value),
        new MoodNumber("fogHeight", MoodGroup.Fog, Environment, mood => mood.FogHeight, (mood, value) => mood.FogHeight = value),
        new MoodNumber("fogHeightFalloff", MoodGroup.Fog, Environment, mood => mood.FogHeightFalloff, (mood, value) => mood.FogHeightFalloff = value),
        new MoodNumber("ambientOcclusion", MoodGroup.Sky, Environment, mood => mood.AmbientOcclusion, (mood, value) => mood.AmbientOcclusion = value),
        new MoodNumber("ambientOcclusionRadius", MoodGroup.Sky, Environment, mood => mood.AmbientOcclusionRadius, (mood, value) => mood.AmbientOcclusionRadius = value),
        new MoodNumber("contactShadowLength", MoodGroup.Sun, Environment, mood => mood.ContactShadowLength, (mood, value) => mood.ContactShadowLength = value),
        new MoodNumber("bloom", MoodGroup.Bloom, Lens, mood => mood.Bloom, (mood, value) => mood.Bloom = value),
        new MoodNumber("exposure", MoodGroup.Grade, Environment, mood => mood.Exposure, (mood, value) => mood.Exposure = value),
        new MoodNumber("contrast", MoodGroup.Grade, Environment, mood => mood.Contrast, (mood, value) => mood.Contrast = value),
        new MoodNumber("saturation", MoodGroup.Grade, Environment, mood => mood.Saturation, (mood, value) => mood.Saturation = value),
        new MoodColor("tint", MoodGroup.Grade, Environment, mood => mood.Tint, (mood, value) => mood.Tint = value),
        new MoodChoice<Tonemapper>("tonemapper", MoodGroup.Tonemapper, Lens, mood => mood.Tonemapper, (mood, value) => mood.Tonemapper = value),
        new MoodNumber("whitePoint", MoodGroup.Tonemapper, Lens, mood => mood.WhitePoint, (mood, value) => mood.WhitePoint = value),
        new MoodChoice<string?>("canvas", MoodGroup.Canvas, Lens, mood => mood.Canvas, (mood, value) => mood.Canvas = value),
        new MoodNumber("canvasShading", MoodGroup.Canvas, Lens, mood => mood.CanvasShading, (mood, value) => mood.CanvasShading = value),
        new MoodNumber("depthNearStart", MoodGroup.Depth, Lens, mood => mood.DepthNearStart, (mood, value) => mood.DepthNearStart = value),
        new MoodNumber("depthNearEnd", MoodGroup.Depth, Lens, mood => mood.DepthNearEnd, (mood, value) => mood.DepthNearEnd = value),
        new MoodNumber("depthNearAmount", MoodGroup.Depth, Lens, mood => mood.DepthNearAmount, (mood, value) => mood.DepthNearAmount = value),
        new MoodNumber("depthFarStart", MoodGroup.Depth, Lens, mood => mood.DepthFarStart, (mood, value) => mood.DepthFarStart = value),
        new MoodNumber("depthFarEnd", MoodGroup.Depth, Lens, mood => mood.DepthFarEnd, (mood, value) => mood.DepthFarEnd = value),
        new MoodNumber("depthFarAmount", MoodGroup.Depth, Lens, mood => mood.DepthFarAmount, (mood, value) => mood.DepthFarAmount = value),
        new MoodColor("depthFarColor", MoodGroup.Depth, Lens, mood => mood.DepthFarColor, (mood, value) => mood.DepthFarColor = value),
    ];

    private static readonly Dictionary<string, MoodValue> ByName = Values.ToDictionary(value => value.Name);

    public static IReadOnlyList<MoodValue> All => Values;

    /// <summary>The value files spell with a name, or none.</summary>
    public static MoodValue? Find(string name) => ByName.GetValueOrDefault(name);
}
