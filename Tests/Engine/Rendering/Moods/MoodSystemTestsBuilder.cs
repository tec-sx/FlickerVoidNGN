using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Rendering.Tests.Moods;

/// <summary>
/// Builds a game with rendering and a mood system over a temporary content folder holding a day, a night and a storm mood,
/// which differ in exposure, and an environment whose exposure climbs from midnight to noon. The night rule holds while the
/// Story.Night fact is at least 1; the storm rule needs it higher.
/// </summary>
internal sealed class MoodSystemTestsBuilder : IDisposable
{
    public const string Day = "Day.fvmood";
    public const string Night = "Night.fvmood";
    public const string Storm = "Storm.fvmood";
    public const string Street = "Street.fvenv";
    public const float DayExposure = 1.1f;
    public const float NightExposure = 0.4f;
    public const float StormExposure = 0.2f;

    private static readonly Tag NightFact = new("Story.Night");

    private readonly string _contentFolder = Path.Combine(Path.GetTempPath(), $"mood-system-{Guid.NewGuid():N}");
    private readonly ILogger<MoodSystem> _logger = Substitute.For<ILogger<MoodSystem>>();
    private readonly List<MoodRule> _rules = [];
    private readonly ContentManager _content;
    private MoodSystem? _moods;

    public MoodSystemTestsBuilder()
    {
        Directory.CreateDirectory(_contentFolder);
        _content = new ContentManager(_contentFolder);
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        Game = new Game(NullLoggerFactory.Instance);
        Game.AddRendering();
        WithFile(Day, $$"""{ "exposure": {{DayExposure}} }""");
        WithFile(Night, $$"""{ "exposure": {{NightExposure}} }""");
        WithFile(Storm, $$"""{ "exposure": {{StormExposure}} }""");
        WithFile(Street, MoodDirectorTestsBuilder.RisingExposure);
    }

    public Game Game { get; }

    /// <summary>The warnings the mood system logged.</summary>
    public IEnumerable<object?[]> Warnings => _logger.ReceivedCalls()
        .Where(static call => call.GetMethodInfo().Name == nameof(ILogger.Log))
        .Select(static call => call.GetArguments())
        .Where(static arguments => arguments[0] is LogLevel.Warning);

    public MoodSystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        File.WriteAllText(fullPath, json);
        return this;
    }

    public MoodSystemTestsBuilder WithNightRule() => WithRule(Night, 1);

    public MoodSystemTestsBuilder WithStormRule(int minimumNight) => WithRule(Storm, minimumNight);

    /// <summary>Spawns an entity with the components, as a scene file would.</summary>
    public Entity Spawn(string componentsJson)
    {
        string json = $$"""{ "entities": [ { "name": "Lighting", "components": {{componentsJson}} } ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        return spawned[0];
    }

    /// <summary>Rewrites a file and reports it changed, as the content watcher would.</summary>
    public void EditFile(string path, string json)
    {
        WithFile(path, json);
        _content.NotifyChanged(path);
    }

    public void SetNight(int value)
    {
        Game.Facts.SetFact(NightFact, value);
    }

    /// <summary>Runs the mood system for a frame of the seconds given.</summary>
    public void Update(float seconds = 0)
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
        GameTime time = new(elapsed, elapsed, 1);
        _moods!.Update(in time);
    }

    public MoodSystem Build()
    {
        _moods = new MoodSystem(Game, _content, _logger) { Rules = _rules };
        return _moods;
    }

    public void Dispose()
    {
        _moods?.Dispose();
        Game.Dispose();
        Directory.Delete(_contentFolder, recursive: true);
    }

    private MoodSystemTestsBuilder WithRule(string mood, int minimumNight)
    {
        FactCondition condition = new() { Fact = NightFact, Compare = FactCompare.GreaterOrEqual, Value = minimumNight };
        ConditionSet when = new() { Conditions = [condition] };
        _rules.Add(new MoodRule { When = when, Mood = mood });
        return this;
    }
}
