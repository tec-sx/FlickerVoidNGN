using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>
/// Keeps a game's mood on screen (a frame system and service): it gives its <see cref="MoodDirector"/> the environment and
/// the mood the scene names, and the mood of the hardest story rule that holds, and builds the mood each frame at the world
/// clock's hour. A scene's environment also sets the clock's speed when the scene plays. A file edited on disk applies at
/// once; one that can't be read is logged, and its last version stays.
/// </summary>
public sealed partial class MoodSystem : BaseSystem<World, GameTime>
{
    private readonly Game _game;
    private readonly IContentManager _content;
    private readonly ILogger<MoodSystem> _logger;
    private IReadOnlyList<MoodRule> _rules = [];
    private ConditionSet[] _conditions = [];
    private string? _sceneMood;
    private string? _environment;
    private string? _storyMood;
    private bool _storyChanged = true;

    public MoodSystem(Game game, IContentManager content, ILogger<MoodSystem> logger)
        : base(game.World)
    {
        _game = game;
        _content = content;
        _logger = logger;
        _game.Facts.FactsChanged += HandleFactsChanged;
        _content.Changed += HandleContentChanged;
        _game.Lifecycle.OnActivate((Entity _, ref SceneEnvironment environment) => StartClock(environment));
    }

    public MoodDirector Director { get; } = new();

    /// <summary>The mood to draw with now.</summary>
    public LightingMood Current => Director.Current;

    /// <summary>A mood file drawn in place of the one the scene names, such as the host's <c>--mood</c>; null follows the scene.</summary>
    public string? SceneMoodPath { get; set; }

    /// <summary>The seconds a change of the story's mood blends over.</summary>
    public float StoryBlendSeconds { get; set; } = 2;

    /// <summary>The story's moods: the mood of the hardest rule that holds lies over the scene's, and none when no rule holds.</summary>
    public IReadOnlyList<MoodRule> Rules
    {
        get => _rules;
        set
        {
            _rules = value;
            _conditions = [.. value.Select(rule => rule.When)];
            _storyChanged = true;
        }
    }

    /// <summary>The mood file as the values it names, or null, logged, when it is missing or can't be read.</summary>
    public MoodOverride? LoadMood(string path)
    {
        try
        {
            return _content.Load(path, RenderingJsonContext.Default.MoodOverride);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            LogUnreadable(path, exception.Message);
            return null;
        }
    }

    public override void Update(in GameTime t)
    {
        FollowScene();
        FollowStory();
        float seconds = (float)t.Elapsed.TotalSeconds;
        Director.Update(_game.Clock.Hour, seconds);
    }

    public override void Dispose()
    {
        _game.Facts.FactsChanged -= HandleFactsChanged;
        _content.Changed -= HandleContentChanged;
    }

    /// <summary>Takes the scene's mood and environment when the files it names change, as on travel to another scene, at once.</summary>
    private void FollowScene()
    {
        string? mood = SceneMoodPath ?? SceneMood.Find(World);
        if (mood != _sceneMood)
        {
            _sceneMood = mood;
            MoodOverride? loaded = LoadMoodOrNone(mood);
            Director.SetLayer(MoodLayer.Scene, loaded, 0);
        }

        string? environment = SceneEnvironment.Find(World);
        if (environment != _environment)
        {
            _environment = environment;
            LightingEnvironment? loaded = LoadEnvironment(environment);
            Director.SetEnvironment(loaded, 0);
        }
    }

    private void FollowStory()
    {
        if (!_storyChanged)
        {
            return;
        }

        _storyChanged = false;
        string? mood = ChooseStoryMood();
        if (mood == _storyMood)
        {
            return;
        }

        _storyMood = mood;
        MoodOverride? loaded = LoadMoodOrNone(mood);
        Director.SetLayer(MoodLayer.Story, loaded, StoryBlendSeconds);
    }

    private string? ChooseStoryMood()
    {
        if (_conditions.Length == 0)
        {
            return null;
        }

        ConditionContext context = new(_game.Facts);
        int[] held = ConditionSet.Rank(_conditions, context);
        if (held.Length == 0)
        {
            return null;
        }

        return _rules[held[0]].Mood;
    }

    private MoodOverride? LoadMoodOrNone(string? path)
    {
        if (path is null)
        {
            return null;
        }

        return LoadMood(path);
    }

    private LightingEnvironment? LoadEnvironment(string? path)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            return _content.Load(path, RenderingJsonContext.Default.LightingEnvironment);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            LogUnreadable(path, exception.Message);
            return null;
        }
    }

    /// <summary>Sets the clock's speed for the scene, and its hour when no scene or save has set the time yet.</summary>
    private void StartClock(SceneEnvironment environment)
    {
        WorldClock clock = _game.Clock;
        clock.Speed = environment.Speed;
        if (clock.Day == 0 && clock.Hour == 0)
        {
            clock.Set(1, environment.StartHour);
        }
    }

    private void HandleFactsChanged()
    {
        _storyChanged = true;
    }

    /// <summary>Reads a changed file again when it is on screen, keeping its last version when the new one can't be read.</summary>
    private void HandleContentChanged(string fullPath)
    {
        if (IsFile(_sceneMood, fullPath) && LoadMood(_sceneMood!) is { } sceneMood)
        {
            Director.SetLayer(MoodLayer.Scene, sceneMood, 0);
        }

        if (IsFile(_storyMood, fullPath) && LoadMood(_storyMood!) is { } storyMood)
        {
            Director.SetLayer(MoodLayer.Story, storyMood, 0);
        }

        if (IsFile(_environment, fullPath) && LoadEnvironment(_environment) is { } environment)
        {
            Director.SetEnvironment(environment, 0);
        }
    }

    private bool IsFile(string? path, string fullPath)
    {
        if (path is null)
        {
            return false;
        }

        string pathOnDisk = _content.GetFullPath(path);
        return ContentManager.PathComparer.Equals(pathOnDisk, fullPath);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Drawing without {Path}: {Problem}")]
    private partial void LogUnreadable(string path, string problem);
}
