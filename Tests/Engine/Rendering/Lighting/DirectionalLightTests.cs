using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Tests.Lighting;

public sealed class DirectionalLightTests
{
    private const float Precision = 1e-4f;

    private readonly DirectionalLightTestsBuilder _builder = new();

    [Fact]
    public void IsTheSunWhileItIsAboveTheHorizon()
    {
        LightingMood mood = _builder.WithSunAt(new Vector3(0, 0.5f, 1)).WithSun(new Vector3(1, 0.8f, 0.6f), 2).Build();

        DirectionalLight light = DirectionalLight.Of(mood);

        light.Direction.Should().Be(mood.SunDirection);
        light.Light.X.Should().BeApproximately(2, Precision);
        light.Light.Z.Should().BeApproximately(1.2f, Precision);
    }

    [Fact]
    public void KeepsTheSunAtTheHorizonItself()
    {
        LightingMood mood = _builder.WithSunAt(new Vector3(0, 0, 1)).Build();

        DirectionalLight light = DirectionalLight.Of(mood);

        light.Light.Should().Be(Vector3.One, "a sun on the horizon still lights walls facing it");
    }

    [Fact]
    public void IsTheMoonOnceTheSunHasSet()
    {
        LightingMood mood = _builder
            .WithSunAt(new Vector3(0, -0.5f, 1))
            .WithMoonAt(new Vector3(0, 1, -1))
            .WithMoon(new Vector3(0.5f, 0.6f, 0.8f), 0.5f)
            .Build();

        DirectionalLight light = DirectionalLight.Of(mood);

        light.Direction.Should().Be(mood.MoonDirection);
        light.Light.X.Should().BeApproximately(0.25f * DirectionalLight.Lit(mood), Precision);
        light.Light.Z.Should().BeApproximately(0.4f * DirectionalLight.Lit(mood), Precision);
    }

    [Fact]
    public void ShinesBrighterTheFullerTheMoon()
    {
        LightingMood full = _builder.WithSunAt(new Vector3(0, -0.5f, 1)).WithMoonAt(new Vector3(0, 0.5f, -1)).Build();
        LightingMood crescent = new DirectionalLightTestsBuilder().WithSunAt(new Vector3(0, -0.5f, 1)).WithMoonAt(new Vector3(0, 0.1f, 1)).Build();

        float fullLight = DirectionalLight.Of(full).Light.X;
        float crescentLight = DirectionalLight.Of(crescent).Light.X;

        DirectionalLight.Lit(full).Should().BeApproximately(1, Precision, "a moon opposite the sun is lit whole");
        crescentLight.Should().BeLessThan(fullLight * 0.5f);
    }

    [Fact]
    public void GoesDarkWhenTheMoonIsDownToo()
    {
        LightingMood mood = _builder.WithSunAt(new Vector3(0, -0.5f, 1)).WithMoonAt(new Vector3(0, -0.3f, -1)).Build();

        DirectionalLight light = DirectionalLight.Of(mood);

        light.Light.Should().Be(Vector3.Zero, "neither lights the world from under the ground");
    }
}
