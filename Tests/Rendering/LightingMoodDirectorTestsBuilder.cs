using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Rendering.Lighting;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// Builds a mood director over a day, a night and a storm mood file, which differ in exposure. The night rule holds while
/// the Story.Night fact is at least 1; the storm rule needs it higher.
/// </summary>
internal sealed class LightingMoodDirectorTestsBuilder : IDisposable
{
    public const string Day = "Day.fvmood";
    public const string Night = "Night.fvmood";
    public const string Storm = "Storm.fvmood";
    public const float DayExposure = 1;
    public const float NightExposure = 0.4f;
    public const float StormExposure = 0.2f;

    private static readonly Tag NightFact = new("Story.Night");

    private readonly LightingMoodTestsBuilder _files = new();
    private readonly FactDatabase _facts = new(new FactSettings());
    private readonly List<MoodRule> _rules = [];
    private readonly ILogger<LightingMoodDirector> _logger = Substitute.For<ILogger<LightingMoodDirector>>();
    private LightingMoodDirector? _director;

    public LightingMoodDirectorTestsBuilder()
    {
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _files.WithFile(Day, $$"""{ "exposure": {{DayExposure}} }""");
        _files.WithFile(Night, $$"""{ "exposure": {{NightExposure}} }""");
        _files.WithFile(Storm, $$"""{ "exposure": {{StormExposure}} }""");
    }

    public LightingMoodDirectorTestsBuilder WithNightRule()
    {
        return WithRule(Night, 1);
    }

    public LightingMoodDirectorTestsBuilder WithStormRule(int minimumNight)
    {
        return WithRule(Storm, minimumNight);
    }

    /// <summary>The warnings the director logged.</summary>
    public IEnumerable<object?[]> Warnings => _logger.ReceivedCalls()
        .Where(static call => call.GetMethodInfo().Name == nameof(ILogger.Log))
        .Select(static call => call.GetArguments())
        .Where(static arguments => arguments[0] is LogLevel.Warning);

    /// <summary>Rewrites a mood file and reports it changed, as the content watcher would.</summary>
    public void EditFile(string path, string json)
    {
        _files.WithFile(path, json);
        _files.Content.NotifyChanged(path);
    }

    public void SetNight(int value)
    {
        _facts.SetFact(NightFact, value);
    }

    public LightingMoodDirector Build()
    {
        _director = new LightingMoodDirector(_facts, _files.Content, Day, _rules, _logger);
        return _director;
    }

    public void Dispose()
    {
        _director?.Dispose();
        _files.Dispose();
    }

    private LightingMoodDirectorTestsBuilder WithRule(string mood, int minimumNight)
    {
        FactCondition condition = new() { Fact = NightFact, Compare = FactCompare.GreaterOrEqual, Value = minimumNight };
        ConditionSet when = new() { Conditions = [condition] };
        _rules.Add(new MoodRule { When = when, Mood = mood });
        return this;
    }
}
