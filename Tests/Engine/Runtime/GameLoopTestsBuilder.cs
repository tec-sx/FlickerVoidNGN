using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Runtime.Tests;

/// <summary>Builds the game loop under test.</summary>
internal sealed class GameLoopTestsBuilder
{
    private readonly List<Action<GameTime>> _fixedUpdates = [];
    private readonly List<Action<GameTime>> _updates = [];

    public GameLoopTestsBuilder OnFixedUpdate(Action<GameTime> handler)
    {
        _fixedUpdates.Add(handler);
        return this;
    }

    public GameLoopTestsBuilder OnUpdate(Action<GameTime> handler)
    {
        _updates.Add(handler);
        return this;
    }

    public GameLoop Build()
    {
        GameLoop loop = new();
        foreach (Action<GameTime> handler in _fixedUpdates)
        {
            loop.FixedUpdate += handler;
        }

        foreach (Action<GameTime> handler in _updates)
        {
            loop.Update += handler;
        }

        return loop;
    }
}
