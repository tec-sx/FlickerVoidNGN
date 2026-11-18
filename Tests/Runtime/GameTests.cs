using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Runtime;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Runtime.Tests;

public class GameTests
{
    [Fact]
    public void RegistersWorldAndLoggerFactoryAsServices()
    {
        using Game game = new();

        World? world = game.Services.GetService<World>();
        ILoggerFactory? loggerFactory = game.Services.GetService<ILoggerFactory>();

        Assert.Same(game.World, world);
        Assert.Same(game.LoggerFactory, loggerFactory);
    }

    [Fact]
    public void RunsFixedSystemsBeforeFrameSystems()
    {
        using Game game = new();
        List<string> calls = [];
        game.FixedSystems.Add(new RecordingSystem("fixed", calls));
        game.FrameSystems.Add(new RecordingSystem("frame", calls));

        game.Tick(game.Loop.FixedStep * 2);

        Assert.Equal(
            [
                "fixed.Initialize", "frame.Initialize",
                "fixed.Before", "fixed.Update", "fixed.After",
                "fixed.Before", "fixed.Update", "fixed.After",
                "frame.Before", "frame.Update", "frame.After",
            ],
            calls);
    }

    [Fact]
    public void InitializesSystemsOnce()
    {
        using Game game = new();
        List<string> calls = [];
        game.FrameSystems.Add(new RecordingSystem("frame", calls));

        game.Tick(TimeSpan.Zero);
        game.Tick(TimeSpan.Zero);

        int initializations = calls.Count(call => call == "frame.Initialize");
        Assert.Equal(1, initializations);
    }

    [Fact]
    public void DisposesSystemsAndWorld()
    {
        List<string> calls = [];
        Game game = new();
        game.FrameSystems.Add(new RecordingSystem("frame", calls));
        World world = game.World;

        game.Dispose();

        Assert.Contains("frame.Dispose", calls);
        Assert.DoesNotContain(world, World.Worlds);
    }

    /// <summary>Records each call as "name.Phase".</summary>
    private sealed class RecordingSystem(string name, List<string> calls) : ISystem<GameTime>
    {
        public void Initialize()
        {
            calls.Add($"{name}.Initialize");
        }

        public void BeforeUpdate(in GameTime t)
        {
            calls.Add($"{name}.Before");
        }

        public void Update(in GameTime t)
        {
            calls.Add($"{name}.Update");
        }

        public void AfterUpdate(in GameTime t)
        {
            calls.Add($"{name}.After");
        }

        public void Dispose()
        {
            calls.Add($"{name}.Dispose");
        }
    }
}
