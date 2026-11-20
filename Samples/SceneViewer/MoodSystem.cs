using Arch.System;

using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Hosting;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Loop;

using Silk.NET.Input;

namespace FlickerVoid.Samples.SceneViewer;

/// <summary>
/// Plays the story's part for the viewer: N steps the Viewer.Mood fact through the mood files, the director's rules turn the
/// fact into a mood, and the renderer draws with the blended mood each frame.
/// </summary>
public sealed class MoodSystem : ISystem<GameTime>
{
    public static readonly Tag MoodFact = new("Viewer.Mood");

    private readonly IGameWindow _window;
    private readonly IFactDatabase _facts;
    private readonly LightingMoodDirector _director;
    private readonly SceneRenderSystem _renderSystem;
    private readonly Func<LightingMood, LightingMood> _configure;
    private readonly int _moodCount;
    private readonly Func<bool> _keyboardBlocked;
    private LightingMood? _applied;

    public MoodSystem(IGameWindow window, IFactDatabase facts, LightingMoodDirector director, SceneRenderSystem renderSystem, Func<LightingMood, LightingMood> configure, int moodCount, Func<bool> keyboardBlocked)
    {
        _window = window;
        _facts = facts;
        _director = director;
        _renderSystem = renderSystem;
        _configure = configure;
        _moodCount = moodCount;
        _keyboardBlocked = keyboardBlocked;
    }

    public void Initialize()
    {
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        if (_window.Input.WasPressed(Key.N) && !_keyboardBlocked())
        {
            int current = _facts.GetFact(MoodFact);
            int next = (current + 1) % _moodCount;
            _facts.SetFact(MoodFact, next);
        }

        float seconds = (float)t.Elapsed.TotalSeconds;
        _director.Update(seconds);
        if (!ReferenceEquals(_director.Current, _applied))
        {
            _renderSystem.Renderer.Mood = _configure(_director.Current);
            _applied = _director.Current;
        }
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
    }
}
