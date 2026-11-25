using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Cameras.Tests.Directing;

public sealed class ShotMoodSystemTests : IDisposable
{
    private const float Precision = 1e-3f;
    private const float Frame = 1 / 60f;

    private readonly ShotMoodSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ChangesOnlyTheValuesTheLiveShotsMoodNames()
    {
        _builder.Build().Run(Frame);

        _builder.Director.Switch(_builder.Cellar, ShotTransition.Cut);
        _builder.Run(Frame);

        LightingMood drawn = _builder.Moods.Current;
        drawn.Exposure.Should().Be(ShotMoodSystemTestsBuilder.CellarExposure);
        drawn.Saturation.Should().Be(ShotMoodSystemTestsBuilder.SceneSaturation, "the cellar's mood names only its exposure");
    }

    [Fact]
    public void ShowsTheMoodOnTheFrameTheShotIsCutTo()
    {
        _builder.Build().Run(Frame);

        _builder.Director.Switch(_builder.Cellar, ShotTransition.Cut);
        _builder.Run(Frame);

        _builder.Moods.Current.Exposure.Should().Be(ShotMoodSystemTestsBuilder.CellarExposure);
    }

    [Fact]
    public void StartsOnTheMoodOfTheShotTheSceneStartsOn()
    {
        _builder.Build();
        _builder.Game.World.Get<Shot>(_builder.Street).Start = false;
        _builder.Game.World.Get<Shot>(_builder.Cellar).Start = true;

        _builder.Run(Frame);

        _builder.Moods.Current.Exposure.Should().Be(ShotMoodSystemTestsBuilder.CellarExposure);
    }

    [Fact]
    public void BlendsToTheMoodAsTheCameraBlendsToTheShot()
    {
        _builder.Build().Run(Frame);

        ShotTransition blend = new() { Kind = TransitionKind.Blend, Duration = 1 };
        _builder.Director.Switch(_builder.Cellar, blend);
        _builder.Run(0.5f);

        float exposure = _builder.Moods.Current.Exposure;
        exposure.Should().BeLessThan(ShotMoodSystemTestsBuilder.SceneExposure);
        exposure.Should().BeGreaterThan(ShotMoodSystemTestsBuilder.CellarExposure);

        _builder.Run(0.6f);
        _builder.Moods.Current.Exposure.Should().BeApproximately(ShotMoodSystemTestsBuilder.CellarExposure, Precision);
    }

    [Fact]
    public void ChangesTheMoodWhileAFadeIsDark()
    {
        _builder.Build().Run(Frame);

        ShotTransition fade = new() { Kind = TransitionKind.Fade, Duration = 0.5f, InDuration = 0.5f };
        _builder.Director.Switch(_builder.Cellar, fade);
        _builder.Run(0.25f);
        _builder.Moods.Current.Exposure.Should().Be(ShotMoodSystemTestsBuilder.SceneExposure, "the cellar is not live while fading out");

        _builder.Run(0.3f);
        _builder.Moods.Current.Exposure.Should().Be(ShotMoodSystemTestsBuilder.CellarExposure, "the mood changes at once while the world is dark");
    }

    [Fact]
    public void BlendsBackToTheScenesMoodWhenTheShotEnds()
    {
        _builder.Build().Run(Frame);
        _builder.Director.Push(_builder.Cellar, ShotTransition.Cut);
        _builder.Run(Frame);

        ShotTransition blend = new() { Kind = TransitionKind.Blend, Duration = 1 };
        _builder.Director.Return(blend);
        _builder.Run(0.5f);
        _builder.Moods.Current.Exposure.Should().BeGreaterThan(ShotMoodSystemTestsBuilder.CellarExposure);

        _builder.Run(0.6f);
        _builder.Moods.Current.Exposure.Should().BeApproximately(ShotMoodSystemTestsBuilder.SceneExposure, Precision);
    }

    [Fact]
    public void AppliesAnEditedShotMoodAtOnce()
    {
        _builder.Build().Run(Frame);
        _builder.Director.Switch(_builder.Cellar, ShotTransition.Cut);
        _builder.Run(Frame);

        _builder.EditFile(ShotMoodSystemTestsBuilder.CellarMoodFile, """{ "exposure": 0.6 }""");
        _builder.Run(Frame);

        _builder.Moods.Current.Exposure.Should().Be(0.6f);
    }

    [Fact]
    public void LeavesTheMoodToTheSceneWhenTheLiveShotGoes()
    {
        _builder.Build().Run(Frame);
        _builder.Director.Switch(_builder.Cellar, ShotTransition.Cut);
        _builder.Run(Frame);

        _builder.Game.World.Destroy(_builder.Cellar);
        _builder.Run(Frame);

        _builder.Moods.Current.Exposure.Should().Be(ShotMoodSystemTestsBuilder.SceneExposure);
    }
}
