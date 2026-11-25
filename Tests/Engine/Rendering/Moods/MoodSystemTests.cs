using Arch.Core;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Moods;

namespace FlickerVoid.Engine.Rendering.Tests.Moods;

public sealed class MoodSystemTests : IDisposable
{
    private const float Precision = 1e-5f;
    private const string DayScene = """{ "sceneMood": { "mood": "Day.fvmood" } }""";
    private const string StreetScene = """{ "sceneEnvironment": { "environment": "Street.fvenv", "speed": 2, "startHour": 9 } }""";

    private readonly MoodSystemTestsBuilder _builder = new();

    [Fact]
    public void ScenesCanNameTheirEnvironment()
    {
        _builder.Game.Spawner.Components.Select(component => component.Name).Should().Contain("sceneEnvironment");
    }

    [Fact]
    public void DrawsWithTheDefaultMoodInASceneThatNamesNothing()
    {
        MoodSystem moods = _builder.Build();

        _builder.Update();

        moods.Current.Should().Be(new LightingMood());
    }

    [Fact]
    public void DrawsASceneThatOnlyNamesAMoodAsThatMood()
    {
        _builder.Spawn(DayScene);
        MoodSystem moods = _builder.Build();

        _builder.Update();

        LightingMood day = new() { Exposure = MoodSystemTestsBuilder.DayExposure };
        moods.Current.Should().Be(day);
    }

    [Fact]
    public void DrawsWithAMoodGivenInPlaceOfTheScenes()
    {
        _builder.Spawn(DayScene);
        MoodSystem moods = _builder.Build();

        moods.SceneMoodPath = MoodSystemTestsBuilder.Night;
        _builder.Update();

        moods.Current.Exposure.Should().Be(MoodSystemTestsBuilder.NightExposure);
    }

    [Fact]
    public void FollowsTheSceneToAnotherMoodAtOnce()
    {
        Entity lighting = _builder.Spawn(DayScene);
        MoodSystem moods = _builder.Build();
        _builder.Update();

        _builder.Game.World.Destroy(lighting);
        _builder.Spawn("""{ "sceneMood": { "mood": "Night.fvmood" } }""");
        _builder.Update(0.01f);

        moods.Current.Exposure.Should().Be(MoodSystemTestsBuilder.NightExposure);
    }

    [Fact]
    public void ChangesItsLightAsTheClockRuns()
    {
        _builder.Spawn(StreetScene);
        MoodSystem moods = _builder.Build();
        _builder.Game.Clock.Set(1, 0);
        _builder.Update();
        float midnight = moods.Current.Exposure;

        _builder.Game.Clock.Set(1, 6);
        _builder.Update(0.01f);

        midnight.Should().BeApproximately(0.4f, Precision);
        moods.Current.Exposure.Should().BeApproximately(0.8f, Precision);
    }

    [Fact]
    public void LaysTheScenesMoodOverItsEnvironment()
    {
        _builder.WithFile("Dim.fvmood", """{ "ambient": 0.1 }""");
        _builder.Spawn("""{ "sceneEnvironment": { "environment": "Street.fvenv" }, "sceneMood": { "mood": "Dim.fvmood" } }""");
        MoodSystem moods = _builder.Build();
        _builder.Game.Clock.Set(1, 12);

        _builder.Update();

        moods.Current.Exposure.Should().BeApproximately(1.2f, Precision);
        moods.Current.Ambient.Should().Be(0.1f);
    }

    [Fact]
    public void StartsTheClockAtTheFirstScenesHourAndSpeedWhenPlayBegins()
    {
        _builder.Build();
        _builder.Spawn(StreetScene);

        _builder.Game.Lifecycle.Play(_builder.Game.World);

        _builder.Game.Clock.Day.Should().Be(1);
        _builder.Game.Clock.Hour.Should().Be(9);
        _builder.Game.Clock.Speed.Should().Be(2);
    }

    [Fact]
    public void KeepsTheTimeAndTakesTheSpeedWhenALaterSceneStarts()
    {
        _builder.Build();
        _builder.Game.Clock.Set(2, 15.5);
        _builder.Spawn(StreetScene);

        _builder.Game.Lifecycle.Play(_builder.Game.World);

        _builder.Game.Clock.Day.Should().Be(2);
        _builder.Game.Clock.Hour.Should().Be(15.5);
        _builder.Game.Clock.Speed.Should().Be(2);
    }

    [Fact]
    public void LeavesTheClockAloneOutsidePlay()
    {
        _builder.Build();

        _builder.Spawn(StreetScene);

        _builder.Game.Clock.Speed.Should().Be(0);
        _builder.Game.Clock.Hour.Should().Be(0);
    }

    [Fact]
    public void StartsOnTheMoodOfARuleThatAlreadyHolds()
    {
        _builder.Spawn(DayScene);
        _builder.SetNight(1);
        MoodSystem moods = _builder.WithNightRule().Build();

        _builder.Update();

        moods.Current.Exposure.Should().Be(MoodSystemTestsBuilder.NightExposure);
    }

    [Fact]
    public void BlendsToTheMoodWhoseRuleHoldsWhenAFactChanges()
    {
        _builder.Spawn(DayScene);
        MoodSystem moods = _builder.WithNightRule().Build();
        _builder.Update();

        _builder.SetNight(1);
        _builder.Update(moods.StoryBlendSeconds / 2);
        float halfway = moods.Current.Exposure;
        _builder.Update(moods.StoryBlendSeconds / 2);

        float between = (MoodSystemTestsBuilder.DayExposure + MoodSystemTestsBuilder.NightExposure) / 2;
        halfway.Should().BeApproximately(between, Precision);
        moods.Current.Exposure.Should().Be(MoodSystemTestsBuilder.NightExposure);
    }

    [Fact]
    public void ReturnsToTheScenesMoodWhenNoRuleHolds()
    {
        _builder.Spawn(DayScene);
        MoodSystem moods = _builder.WithNightRule().Build();
        _builder.SetNight(1);
        _builder.Update();

        _builder.SetNight(0);
        _builder.Update(moods.StoryBlendSeconds);

        moods.Current.Exposure.Should().Be(MoodSystemTestsBuilder.DayExposure);
    }

    [Fact]
    public void PrefersTheHardestRuleThatHolds()
    {
        MoodSystem moods = _builder.WithNightRule().WithStormRule(minimumNight: 2).Build();

        _builder.SetNight(3);
        _builder.Update();

        moods.Current.Exposure.Should().Be(MoodSystemTestsBuilder.StormExposure);
    }

    [Fact]
    public void ChangesOnlyWhatTheStorysMoodNames()
    {
        _builder.WithFile("Dim.fvmood", """{ "ambient": 0.1, "exposure": 0.9 }""");
        _builder.Spawn("""{ "sceneMood": { "mood": "Dim.fvmood" } }""");
        MoodSystem moods = _builder.WithNightRule().Build();

        _builder.SetNight(1);
        _builder.Update();

        moods.Current.Exposure.Should().Be(MoodSystemTestsBuilder.NightExposure);
        moods.Current.Ambient.Should().Be(0.1f, "the night mood names only its exposure");
    }

    [Fact]
    public void AppliesAnEditedMoodAtOnce()
    {
        _builder.Spawn(DayScene);
        MoodSystem moods = _builder.Build();
        _builder.Update();

        _builder.EditFile(MoodSystemTestsBuilder.Day, """{ "exposure": 1.6 }""");
        _builder.Update(0.01f);

        moods.Current.Exposure.Should().Be(1.6f);
    }

    [Fact]
    public void AppliesAnEditedEnvironmentAtOnce()
    {
        _builder.Spawn(StreetScene);
        MoodSystem moods = _builder.Build();
        _builder.Update();

        _builder.EditFile(MoodSystemTestsBuilder.Street, """{ "numbers": { "exposure": { "keys": [ { "time": 0, "value": 0.25 } ] } } }""");
        _builder.Update(0.01f);

        moods.Current.Exposure.Should().Be(0.25f);
    }

    [Fact]
    public void KeepsTheLastVersionAndLogsWhenAnEditedMoodCannotBeRead()
    {
        _builder.Spawn(DayScene);
        MoodSystem moods = _builder.Build();
        _builder.Update();

        _builder.EditFile(MoodSystemTestsBuilder.Day, """{ "exposure": """);
        _builder.Update(0.01f);

        moods.Current.Exposure.Should().Be(MoodSystemTestsBuilder.DayExposure);
        _builder.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void LaysTheMainCamerasLensUnderTheScenesMood()
    {
        _builder.WithFile("Main.fvlens", """{ "bloom": 0.3, "depthNearAmount": 0.4 }""");
        _builder.WithFile("Glow.fvmood", """{ "bloom": 0.6 }""");
        _builder.Spawn("""{ "mainCamera": { "lens": "Main.fvlens" }, "sceneMood": { "mood": "Glow.fvmood" } }""");
        MoodSystem moods = _builder.Build();

        _builder.Update();

        moods.Current.DepthNearAmount.Should().Be(0.4f);
        moods.Current.Bloom.Should().Be(0.6f, "the scene's mood lies over the lens");
    }

    [Fact]
    public void AppliesAnEditedLensAtOnce()
    {
        _builder.WithFile("Main.fvlens", """{ "bloom": 0.3 }""");
        _builder.Spawn("""{ "mainCamera": { "lens": "Main.fvlens" } }""");
        MoodSystem moods = _builder.Build();
        _builder.Update();

        _builder.EditFile("Main.fvlens", """{ "bloom": 0.5 }""");
        _builder.Update(0.01f);

        moods.Current.Bloom.Should().Be(0.5f);
    }

    [Fact]
    public void ShowsAShotsMoodAtOnceWhenItCutsEvenAfterTheFramesUpdate()
    {
        _builder.Spawn(DayScene);
        MoodSystem moods = _builder.Build();
        _builder.Update();

        moods.SetShotMood(MoodSystemTestsBuilder.Night, 0);

        moods.Current.Exposure.Should().Be(MoodSystemTestsBuilder.NightExposure);
    }

    [Fact]
    public void BlendsToAShotsMoodOverTheSecondsGiven()
    {
        _builder.Spawn(DayScene);
        MoodSystem moods = _builder.Build();
        _builder.Update();

        moods.SetShotMood(MoodSystemTestsBuilder.Night, 1);
        _builder.Update(0.5f);

        float halfway = (MoodSystemTestsBuilder.DayExposure + MoodSystemTestsBuilder.NightExposure) / 2;
        moods.Current.Exposure.Should().BeApproximately(halfway, Precision);
    }

    [Fact]
    public void ReturnsToTheScenesMoodWhenTheShotsMoodIsTakenOff()
    {
        _builder.Spawn(DayScene);
        MoodSystem moods = _builder.Build();
        _builder.Update();
        moods.SetShotMood(MoodSystemTestsBuilder.Night, 0);

        moods.SetShotMood(null, 0);

        moods.Current.Exposure.Should().Be(MoodSystemTestsBuilder.DayExposure);
    }

    [Fact]
    public void LaysTheShotsMoodOverTheStorys()
    {
        _builder.WithNightRule();
        MoodSystem moods = _builder.Build();
        _builder.SetNight(1);
        _builder.Update();

        moods.SetShotMood(MoodSystemTestsBuilder.Storm, 0);

        moods.Current.Exposure.Should().Be(MoodSystemTestsBuilder.StormExposure);
    }

    [Fact]
    public void AppliesAnEditedShotMoodAtOnce()
    {
        MoodSystem moods = _builder.Build();
        _builder.Update();
        moods.SetShotMood(MoodSystemTestsBuilder.Night, 0);

        _builder.EditFile(MoodSystemTestsBuilder.Night, """{ "exposure": 0.7 }""");
        _builder.Update(0.01f);

        moods.Current.Exposure.Should().Be(0.7f);
    }

    [Fact]
    public void DrawsWithoutAMoodThatIsMissingAndLogsIt()
    {
        _builder.Spawn("""{ "sceneMood": { "mood": "Lost.fvmood" } }""");
        MoodSystem moods = _builder.Build();

        _builder.Update();
        _builder.Update(0.01f);

        moods.Current.Should().Be(new LightingMood());
        _builder.Warnings.Should().ContainSingle("a missing file is reported once, not every frame");
    }

    public void Dispose()
    {
        _builder.Dispose();
    }
}
