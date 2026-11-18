using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Runtime.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Runtime;

/// <summary>The running game: its entity world, services and systems, which the game loop advances in fixed steps and frames.</summary>
public sealed partial class Game : IDisposable
{
    private readonly ILogger _logger;
    private bool _initialized;

    /// <summary>Creates the world and registers it, the logger factory and the scene spawner as services.</summary>
    public Game(ILoggerFactory? loggerFactory = null)
    {
        LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = LoggerFactory.CreateLogger<Game>();
        World = World.Create();

        Services.AddService(LoggerFactory);
        Services.AddService(World);

        Spawner.Register("transform", RuntimeJsonContext.Default.Transform);
        Services.AddService(Spawner);

        Loop.FixedUpdate += HandleFixedUpdate;
        Loop.Update += HandleUpdate;
    }

    public ILoggerFactory LoggerFactory { get; }

    public World World { get; }

    public ServiceRegistry Services { get; } = new();

    /// <summary>Spawns scene files into the world; modules register their components on it.</summary>
    public SceneSpawner Spawner { get; } = new();

    public GameLoop Loop { get; } = new();

    /// <summary>Systems run once per fixed step: simulation, physics and gameplay rules that must not depend on the frame rate.</summary>
    public Group<GameTime> FixedSystems { get; } = new("Fixed");

    /// <summary>Systems run once per frame, after the frame's fixed steps: input, animation, presentation.</summary>
    public Group<GameTime> FrameSystems { get; } = new("Frame");

    /// <summary>Advances the game by the real time since the last tick; the first tick initializes the systems.</summary>
    public void Tick(TimeSpan elapsed)
    {
        if (!_initialized)
        {
            FixedSystems.Initialize();
            FrameSystems.Initialize();
            _initialized = true;
        }

        if (elapsed > Loop.MaxElapsed)
        {
            LogFrameClamped(elapsed.TotalMilliseconds, Loop.MaxElapsed.TotalMilliseconds);
        }

        Loop.Advance(elapsed);
    }

    public void Dispose()
    {
        Loop.FixedUpdate -= HandleFixedUpdate;
        Loop.Update -= HandleUpdate;
        FrameSystems.Dispose();
        FixedSystems.Dispose();
        World.Destroy(World);
    }

    private static void Run(Group<GameTime> systems, GameTime time)
    {
        systems.BeforeUpdate(in time);
        systems.Update(in time);
        systems.AfterUpdate(in time);
    }

    private void HandleFixedUpdate(GameTime time)
    {
        Run(FixedSystems, time);
    }

    private void HandleUpdate(GameTime time)
    {
        Run(FrameSystems, time);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A frame took {Milliseconds:F0} ms; the game advanced {MaxMilliseconds:F0} ms.")]
    private partial void LogFrameClamped(double milliseconds, double maxMilliseconds);
}
