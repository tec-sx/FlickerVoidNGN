using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Debugging;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Runtime.Services;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Runtime.Loop;

/// <summary>The running game: its entity world, services and systems, which the game loop advances in fixed steps and frames.</summary>
public sealed partial class Game : IDisposable
{
    private readonly ILogger _logger;
    private bool _initialized;

    /// <summary>Creates the world and registers it, the logger factory, the scene spawner and the debug console as services.</summary>
    public Game(ILoggerFactory loggerFactory)
    {
        LoggerFactory = loggerFactory;
        _logger = LoggerFactory.CreateLogger<Game>();
        World = World.Create();

        Services.AddService(LoggerFactory);
        Services.AddService(World);

        Spawner.Register("transform", RuntimeJsonContext.Default.Transform);
        Services.AddService(Spawner);
        Schemas.Add("scene", Spawner.GetSchema);
        Schemas.Add("entity", Spawner.GetTemplateSchema);
        Services.AddService(DebugConsole);
        Services.AddService(Events);

        Loop.FixedUpdate += HandleFixedUpdate;
        Loop.Update += HandleUpdate;
    }

    public ILoggerFactory LoggerFactory { get; }

    public World World { get; }

    public IServiceRegistry Services { get; } = new ServiceRegistry();

    /// <summary>The schemas of the game's text content; modules add the file kinds they read.</summary>
    public ContentSchemas Schemas { get; } = new();

    /// <summary>Spawns scene files into the world; modules register their components on it.</summary>
    public ISceneSpawner Spawner { get; } = new SceneSpawner();

    /// <summary>Starts and stops entities; systems add their components' hooks to it. It is the spawner's, so scenes start what they spawn.</summary>
    public EntityLifecycle Lifecycle => Spawner.Lifecycle;

    /// <summary>Events systems send and handle, dispatched after each group of systems runs.</summary>
    public EventBus Events { get; } = new();

    /// <summary>Debugging commands and variables; modules add theirs to it.</summary>
    public IDebugConsole DebugConsole { get; } = new DebugConsole();

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
        Lifecycle.StopAll(World);
        FrameSystems.Dispose();
        FixedSystems.Dispose();
        World.Destroy(World);
    }

    private void Run(Group<GameTime> systems, GameTime time)
    {
        systems.BeforeUpdate(in time);
        systems.Update(in time);
        systems.AfterUpdate(in time);
        Events.Dispatch();
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

