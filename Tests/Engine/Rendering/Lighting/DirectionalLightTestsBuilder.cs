using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Tests.Lighting;

/// <summary>Builds the mood whose one directional light is picked: the default mood unless told otherwise.</summary>
internal sealed class DirectionalLightTestsBuilder
{
    private LightingMood _mood = new();

    /// <summary>Puts the sun in a direction from the ground; the mood keeps the way its light travels, the opposite.</summary>
    public DirectionalLightTestsBuilder WithSunAt(Vector3 towardSun)
    {
        _mood = _mood with { SunDirection = Vector3.Normalize(-towardSun) };
        return this;
    }

    public DirectionalLightTestsBuilder WithMoonAt(Vector3 towardMoon)
    {
        _mood = _mood with { MoonDirection = Vector3.Normalize(-towardMoon) };
        return this;
    }

    public DirectionalLightTestsBuilder WithSun(Vector3 color, float intensity)
    {
        _mood = _mood with { SunColor = color, SunIntensity = intensity };
        return this;
    }

    public DirectionalLightTestsBuilder WithMoon(Vector3 color, float intensity)
    {
        _mood = _mood with { MoonColor = color, MoonIntensity = intensity };
        return this;
    }

    public LightingMood Build() => _mood;
}
