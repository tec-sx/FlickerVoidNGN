using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class LightingMoodTests : IDisposable
{
    private readonly LightingMoodTestsBuilder _builder = new();

    [Fact]
    public void LoadsAFileThatNamesOnlyWhatItChanges()
    {
        LightingMood mood = _builder
            .WithFile("Dusk.fvmood", """{ "exposure": 1.4, "tint": [1, 0.8, 0.6], "paper": "Paper.png" }""")
            .Load("Dusk.fvmood");

        mood.Exposure.Should().Be(1.4f);
        mood.Tint.Should().Be(new Vector3(1, 0.8f, 0.6f));
        mood.Paper.Should().Be("Paper.png");
        mood.Ambient.Should().Be(new LightingMood().Ambient);
    }

    [Fact]
    public void LoadsAFileThatNamesItsSchema()
    {
        LightingMood mood = _builder
            .WithFile("Dusk.fvmood", """{ "$schema": "../.schemas/mood.schema.json", "exposure": 1.4 }""")
            .Load("Dusk.fvmood");

        mood.Exposure.Should().Be(1.4f);
    }

    [Fact]
    public void BlendsEverySettingBetweenTwoMoods()
    {
        LightingMood day = new() { Exposure = 1, Tint = Vector3.One, FogDensity = 0 };
        LightingMood night = new() { Exposure = 0.5f, Tint = new Vector3(0.5f, 0.5f, 1), FogDensity = 0.2f };

        LightingMood blended = LightingMood.Lerp(day, night, 0.5f);

        blended.Exposure.Should().BeApproximately(0.75f, 1e-6f);
        blended.Tint.Should().Be(new Vector3(0.75f, 0.75f, 1));
        blended.FogDensity.Should().BeApproximately(0.1f, 1e-6f);
    }

    [Fact]
    public void TurnsTheSunRatherThanShorteningIt()
    {
        LightingMood morning = new() { SunDirection = Vector3.Normalize(new Vector3(1, -1, 0)) };
        LightingMood evening = new() { SunDirection = Vector3.Normalize(new Vector3(-1, -1, 0)) };

        LightingMood noon = LightingMood.Lerp(morning, evening, 0.5f);

        noon.SunDirection.X.Should().BeApproximately(0, 1e-6f);
        noon.SunDirection.Y.Should().BeApproximately(-1, 1e-6f);
    }

    [Fact]
    public void SwapsThePaperHalfwayThroughABlend()
    {
        LightingMood plain = new();
        LightingMood paper = new() { Paper = "Paper.png" };

        LightingMood early = LightingMood.Lerp(plain, paper, 0.4f);
        LightingMood late = LightingMood.Lerp(plain, paper, 0.6f);

        early.Paper.Should().BeNull();
        late.Paper.Should().Be("Paper.png");
    }

    public void Dispose()
    {
        _builder.Dispose();
    }
}
