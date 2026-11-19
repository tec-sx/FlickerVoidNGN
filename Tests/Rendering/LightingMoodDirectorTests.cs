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
}
