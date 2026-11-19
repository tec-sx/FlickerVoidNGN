using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// Builds a mood director over a day, a night and a storm mood file, which differ in exposure. The night rule holds while
/// the Story.Night fact is at least 1; the storm rule needs it higher.
/// </summary>
internal sealed class LightingMoodDirectorTestsBuilder : IDisposable
{
    public const string Day = "Day.json";
    public const string Night = "Night.json";
    public const string Storm = "Storm.json";
    public const float DayExposure = 1;
    public const float NightExposure = 0.4f;
    public const float StormExposure = 0.2f;

    private static readonly Tag NightFact = new("Story.Night");

    private readonly LightingMoodTestsBuilder _files = new();
    private readonly FactDatabase _facts = new(new FactSettings());
    private readonly List<MoodRule> _rules = [];
    private LightingMoodDirector? _director;

    public LightingMoodDirectorTestsBuilder()
    {
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

    public void SetNight(int value)
    {
        _facts.SetFact(NightFact, value);
    }

    public LightingMoodDirector Build()
    {
        _director = new LightingMoodDirector(_facts, _files.Content, Day, _rules);
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
