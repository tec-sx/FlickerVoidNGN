using System.Numerics;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Sky;

namespace FlickerVoid.Engine.Rendering.Tests.Moods;

public sealed class MoodDirectorTests
{
    private const float Precision = 1e-5f;

    private readonly MoodDirectorTestsBuilder _builder = new();

    [Fact]
    public void IsTheDefaultMoodWithoutLayers()
    {
        MoodDirector director = _builder.BuildStarted();

        director.Current.Should().Be(new LightingMood());
    }

    [Fact]
    public void LaysEachLayerOverTheOnesUnderIt()
    {
        MoodDirector director = _builder
            .WithEnvironment("""{ "numbers": { "exposure": { "keys": [ { "time": 12, "value": 1.2 } ] }, "fogDensity": { "keys": [ { "time": 12, "value": 0.1 } ] } } }""")
            .WithLayer(MoodLayer.Scene, """{ "exposure": 0.9, "ambient": 0.2 }""")
            .WithLayer(MoodLayer.Story, """{ "exposure": 0.8, "bloom": 0.3 }""")
            .WithLayer(MoodLayer.Shot, """{ "bloom": 0.5 }""")
            .WithLayer(MoodLayer.Debug, """{ "ambient": 0.6 }""")
            .BuildStarted();

        LightingMood expected = new() { FogDensity = 0.1f, Exposure = 0.8f, Bloom = 0.5f, Ambient = 0.6f };
        director.Current.Should().Be(expected);
    }

    [Fact]
    public void FollowsTheEnvironmentAsTheHourMoves()
    {
        MoodDirector director = _builder.WithEnvironment(MoodDirectorTestsBuilder.RisingExposure).BuildStarted();
        LightingMood midnight = director.Current;

        director.Update(6, 0.1f);

        midnight.Exposure.Should().BeApproximately(0.4f, Precision);
        director.Current.Exposure.Should().BeApproximately(0.8f, Precision);
        director.Current.Should().NotBeSameAs(midnight, "a mood handed out is never changed afterwards");
    }

    [Fact]
    public void TurnsTheSunAlongItsPathAsTheHourMoves()
    {
        SunPath path = new(45, 0, 0);
        MoodDirector director = _builder.WithSunPath(path).BuildStarted();

        director.Update(9, 0.1f);

        Vector3 toSun = path.TowardSun(9);
        director.Current.SunDirection.X.Should().BeApproximately(-toSun.X, Precision);
        director.Current.SunDirection.Y.Should().BeApproximately(-toSun.Y, Precision);
    }

    [Fact]
    public void LetsAMoodThatNamesTheSunsDirectionHoldIt()
    {
        MoodDirector director = _builder
            .WithSunPath(new SunPath(45, 0, 0))
            .WithLayer(MoodLayer.Scene, """{ "sunDirection": [0, -1, 0] }""")
            .BuildStarted();

        director.Update(9, 0.1f);

        director.Current.SunDirection.Should().Be(new Vector3(0, -1, 0));
    }

    [Fact]
    public void KeepsTheSameMoodObjectWhileNothingChanges()
    {
        MoodDirector director = _builder
            .WithEnvironment(MoodDirectorTestsBuilder.RisingExposure)
            .WithLayer(MoodLayer.Scene, """{ "ambient": 0.2 }""")
            .BuildStarted();
        LightingMood first = director.Current;

        director.Update(0, 0.1f);

        director.Current.Should().BeSameAs(first);
    }

    [Fact]
    public void KeepsTheSameMoodObjectAsTheHourMovesWithoutAnEnvironment()
    {
        MoodDirector director = _builder.WithLayer(MoodLayer.Scene, """{ "ambient": 0.2 }""").BuildStarted();
        LightingMood first = director.Current;

        director.Update(6, 0.1f);

        director.Current.Should().BeSameAs(first);
    }

    [Fact]
    public void StartsOnItsLayersWithoutBlending()
    {
        MoodDirector director = _builder.Build();
        MoodOverride night = MoodDirectorTestsBuilder.Mood("""{ "exposure": 0.4 }""");

        director.SetLayer(MoodLayer.Story, night, 2);
        director.Update(0, 0);

        director.Current.Exposure.Should().Be(0.4f);
        director.IsBlending.Should().BeFalse();
    }

    [Fact]
    public void BlendsToAChangedLayerOverItsSeconds()
    {
        MoodDirector director = _builder.BuildStarted();
        MoodOverride night = MoodDirectorTestsBuilder.Mood("""{ "exposure": 0.4 }""");

        director.SetLayer(MoodLayer.Story, night, 2);
        director.Update(0, 1);
        float halfway = director.Current.Exposure;
        bool blendingHalfway = director.IsBlending;
        director.Update(0, 1);

        halfway.Should().BeApproximately(0.7f, Precision);
        blendingHalfway.Should().BeTrue();
        director.Current.Exposure.Should().Be(0.4f);
        director.IsBlending.Should().BeFalse();
    }

    [Fact]
    public void ShowsAChangeAtOnceWithoutSeconds()
    {
        MoodDirector director = _builder.BuildStarted();
        MoodOverride night = MoodDirectorTestsBuilder.Mood("""{ "exposure": 0.4 }""");

        director.SetLayer(MoodLayer.Scene, night, 0);
        director.Update(0, 0.01f);

        director.Current.Exposure.Should().Be(0.4f);
    }

    [Fact]
    public void BlendsBackWhenALayerIsTakenAway()
    {
        MoodDirector director = _builder.WithLayer(MoodLayer.Shot, """{ "exposure": 0.4 }""").BuildStarted();

        director.SetLayer(MoodLayer.Shot, null, 1);
        director.Update(0, 0.5f);

        director.Current.Exposure.Should().BeApproximately(0.7f, Precision);
    }

    [Fact]
    public void StartsANewBlendFromTheMoodOnScreen()
    {
        MoodDirector director = _builder.BuildStarted();
        MoodOverride night = MoodDirectorTestsBuilder.Mood("""{ "exposure": 0.4 }""");
        MoodOverride storm = MoodDirectorTestsBuilder.Mood("""{ "exposure": 0.2 }""");

        director.SetLayer(MoodLayer.Story, night, 2);
        director.Update(0, 1);
        director.SetLayer(MoodLayer.Story, storm, 2);
        director.Update(0, 1);

        director.Current.Exposure.Should().BeApproximately(0.45f, Precision, "halfway from the 0.7 on screen to the storm's 0.2");
    }

    [Fact]
    public void LetsTheQuickestOfChangesMadeTogetherDecide()
    {
        MoodDirector director = _builder.BuildStarted();
        MoodOverride cellar = MoodDirectorTestsBuilder.Mood("""{ "ambient": 0.1 }""");
        MoodOverride night = MoodDirectorTestsBuilder.Mood("""{ "exposure": 0.4 }""");

        director.SetLayer(MoodLayer.Story, night, 2);
        director.SetLayer(MoodLayer.Scene, cellar, 0);
        director.Update(0, 0.01f);

        director.Current.Exposure.Should().Be(0.4f, "a cut to another scene is not blended by a story mood that changed with it");
        director.Current.Ambient.Should().Be(0.1f);
    }

    [Fact]
    public void IgnoresALayerSetToWhatItAlreadyIs()
    {
        MoodOverride night = MoodDirectorTestsBuilder.Mood("""{ "exposure": 0.4 }""");
        MoodDirector director = _builder.Build();
        director.SetLayer(MoodLayer.Story, night, 0);
        director.Update(0, 0);
        LightingMood first = director.Current;

        director.SetLayer(MoodLayer.Story, night, 2);
        director.Update(0, 1);

        director.Current.Should().BeSameAs(first);
        director.GetLayer(MoodLayer.Story).Should().BeSameAs(night);
    }

    [Fact]
    public void KeepsFollowingTheHourThroughABlend()
    {
        MoodDirector director = _builder.WithEnvironment(MoodDirectorTestsBuilder.RisingExposure).BuildStarted();
        MoodOverride glow = MoodDirectorTestsBuilder.Mood("""{ "bloom": 0.4 }""");

        director.SetLayer(MoodLayer.Shot, glow, 1);
        director.Update(3, 0.5f);
        float midBlend = director.Current.Exposure;
        director.Update(6, 0.5f);

        midBlend.Should().BeApproximately(0.5f, Precision, "halfway from midnight's 0.4 on screen to the 0.6 of three o'clock");
        director.Current.Exposure.Should().BeApproximately(0.8f, Precision);
        director.Current.Bloom.Should().Be(0.4f);
    }
}
