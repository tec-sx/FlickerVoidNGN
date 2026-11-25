using Arch.System;

using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Runtime.Loop;

using Silk.NET.Input;

namespace FlickerVoid.Samples.SceneViewer;

/// <summary>
/// Plays the story's part for the viewer: N steps the Viewer.Mood fact through the mood files, and the host's mood system
/// turns the fact into a mood through its rules and blends to it.
/// </summary>
public sealed class MoodStepSystem : ISystem<GameTime>
{
    public static readonly Tag MoodFact = new("Viewer.Mood");

    private readonly IGameWindow _window;
    private readonly IFactDatabase _facts;
    private readonly int _moodCount;
    private readonly Func<bool> _keyboardBlocked;

    public MoodStepSystem(IGameWindow window, IFactDatabase facts, int moodCount, Func<bool> keyboardBlocked)
    {
        _window = window;
        _facts = facts;
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
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
    }
}
