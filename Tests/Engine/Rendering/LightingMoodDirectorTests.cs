using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class LightingMoodDirectorTests : IDisposable
{
    private readonly LightingMoodDirectorTestsBuilder _builder = new();

    [Fact]
    public void StartsOnTheDefaultMood()
    {
        LightingMoodDirector director = _builder.WithNightRule().Build();

        director.Target.Should().Be(LightingMoodDirectorTestsBuilder.Day);
        director.Current.Exposure.Should().Be(LightingMoodDirectorTestsBuilder.DayExposure);
    }

    [Fact]
    public void SwitchesToTheMoodWhoseRuleHoldsWhenAFactChanges()
    {
        LightingMoodDirector director = _builder.WithNightRule().Build();

        _builder.SetNight(1);
        director.Update(director.BlendSeconds);

        director.Target.Should().Be(LightingMoodDirectorTestsBuilder.Night);
        director.Current.Exposure.Should().Be(LightingMoodDirectorTestsBuilder.NightExposure);
    }

    [Fact]
    public void BlendsTowardTheNewMoodOverTime()
    {
        LightingMoodDirector director = _builder.WithNightRule().Build();

        _builder.SetNight(1);
        director.Update(director.BlendSeconds / 2);

        float halfway = (LightingMoodDirectorTestsBuilder.DayExposure + LightingMoodDirectorTestsBuilder.NightExposure) / 2;
        director.Current.Exposure.Should().BeApproximately(halfway, 1e-5f);
    }

    [Fact]
    public void ReturnsToTheDefaultWhenNoRuleHolds()
    {
        LightingMoodDirector director = _builder.WithNightRule().Build();
        _builder.SetNight(1);
        director.Update(director.BlendSeconds);

        _builder.SetNight(0);
        director.Update(director.BlendSeconds);

        director.Target.Should().Be(LightingMoodDirectorTestsBuilder.Day);
        director.Current.Exposure.Should().Be(LightingMoodDirectorTestsBuilder.DayExposure);
    }

    [Fact]
    public void PrefersTheHardestRuleThatHolds()
    {
        LightingMoodDirector director = _builder.WithNightRule().WithStormRule(minimumNight: 2).Build();

        _builder.SetNight(2);

        director.Target.Should().Be(LightingMoodDirectorTestsBuilder.Storm);
    }

    [Fact]
    public void StartsANewBlendFromWhereTheLastOneStopped()
    {
        LightingMoodDirector director = _builder.WithNightRule().WithStormRule(minimumNight: 2).Build();
        _builder.SetNight(1);
        director.Update(director.BlendSeconds / 2);
        float before = director.Current.Exposure;

        _builder.SetNight(2);

        director.Current.Exposure.Should().BeApproximately(before, 1e-5f);
    }

    [Fact]
    public void KeepsTheSameMoodObjectOnceTheBlendIsDone()
    {
        LightingMoodDirector director = _builder.WithNightRule().Build();
        _builder.SetNight(1);
        director.Update(director.BlendSeconds);
        LightingMood done = director.Current;

        director.Update(0.5f);

        director.Current.Should().BeSameAs(done);
        director.IsBlending.Should().BeFalse();
    }

    [Fact]
    public void IsBlendingFromAFactChangeUntilTheBlendTimeHasPassed()
    {
        LightingMoodDirector director = _builder.WithNightRule().Build();

        _builder.SetNight(1);
        bool atStart = director.IsBlending;
        director.Update(director.BlendSeconds / 2);
        bool halfway = director.IsBlending;

        atStart.Should().BeTrue();
        halfway.Should().BeTrue();
    }

    public void Dispose()
    {
        _builder.Dispose();
    }

    [Fact]
    public void AppliesTheMoodItShowsAtOnceWhenItsFileIsEdited()
    {
        LightingMoodDirector director = _builder.WithNightRule().Build();

        _builder.EditFile(LightingMoodDirectorTestsBuilder.Day, """{ "exposure": 0.7 }""");

        director.Current.Exposure.Should().Be(0.7f);
        director.IsBlending.Should().BeFalse();
    }

    [Fact]
    public void KeepsTheMoodAndLogsWhenItsEditedFileIsInvalid()
    {
        LightingMoodDirector director = _builder.WithNightRule().Build();

        _builder.EditFile(LightingMoodDirectorTestsBuilder.Day, """{ "exposure": """);

        director.Current.Exposure.Should().Be(LightingMoodDirectorTestsBuilder.DayExposure);
        _builder.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void ReloadsTheMoodItBlendsTowardWhenItsFileIsEdited()
    {
        LightingMoodDirector director = _builder.WithNightRule().Build();
        _builder.SetNight(1);

        _builder.EditFile(LightingMoodDirectorTestsBuilder.Night, """{ "exposure": 0.1 }""");
        director.Update(director.BlendSeconds);

        director.Current.Exposure.Should().Be(0.1f);
    }
}
