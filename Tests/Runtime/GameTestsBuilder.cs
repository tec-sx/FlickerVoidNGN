using Arch.System;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Runtime.Tests;

/// <summary>Builds the game under test with its systems; it logs to a substitute logger factory unless given one.</summary>
internal sealed class GameTestsBuilder
{
    private readonly List<ISystem<GameTime>> _fixedSystems = [];
    private readonly List<ISystem<GameTime>> _frameSystems = [];
    private ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();

    public GameTestsBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    public GameTestsBuilder WithFixedSystem(ISystem<GameTime> system)
    {
        _fixedSystems.Add(system);
        return this;
    }

    public GameTestsBuilder WithFrameSystem(ISystem<GameTime> system)
    {
        _frameSystems.Add(system);
        return this;
    }

    public Game Build()
    {
        Game game = new(_loggerFactory);
        foreach (ISystem<GameTime> system in _fixedSystems)
        {
            game.FixedSystems.Add(system);
        }

        foreach (ISystem<GameTime> system in _frameSystems)
        {
            game.FrameSystems.Add(system);
        }

        return game;
    }
}
