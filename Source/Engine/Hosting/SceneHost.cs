using Arch.Core;

using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Hosting;

/// <summary>
/// Runs a game in a window: a Vulkan device, a game with rendering, its built content (reloaded when it changes), the options'
/// scene and lighting mood, and a <see cref="SceneRenderSystem"/> that draws after every other frame system.
/// </summary>
public sealed partial class SceneHost : IDisposable
{
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera>();

    private readonly HostOptions _options;
    private readonly ILogger<SceneHost> _logger;
    private bool _disposed;

    /// <summary>Creates the device, game and window; add modules and frame systems, then <see cref="Run"/>.</summary>
    public SceneHost(HostOptions options, ILoggerFactory loggerFactory, string title)
    {
        _options = options;
        _logger = loggerFactory.CreateLogger<SceneHost>();
        Content = new ContentManager(options.ContentFolder);
        GraphicsDeviceOptions deviceOptions = new() { Validation = options.Validation };
        Device = new VulkanGraphicsDevice(deviceOptions, loggerFactory);
        Game = new Game(loggerFactory);
        Game.AddRendering();
        Window = new GameWindow(Game, title, 1280, 720);
        ILogger<SceneLoader> sceneLogger = loggerFactory.CreateLogger<SceneLoader>();
        Scenes = new SceneLoader(Game.Spawner, Content, Game.World, sceneLogger);
        ContentWatcher watcher = new(Content, Game.Schemas);
        Game.FrameSystems.Add(watcher);
        if (options.Mood is { } mood)
        {
            Mood = Content.Load(mood, RenderingJsonContext.Default.LightingMood);
        }
    }

    public IGraphicsDevice Device { get; }

    public Game Game { get; }

    public GameWindow Window { get; }

    public ContentManager Content { get; }

    public SceneLoader Scenes { get; }

    /// <summary>The mood the renderer starts with: the options' mood file, else the one the scene names (<see cref="SceneMood"/>), else the default.</summary>
    public LightingMood Mood { get; set; } = new();

    /// <summary>The render system, once <see cref="Start"/> has created it.</summary>
    public SceneRenderSystem? Renderer { get; private set; }

    /// <summary>
    /// Spawns the options' scene and creates the renderer, with an overlay when <paramref name="createOverlay"/> makes one;
    /// <see cref="Run"/> calls it when nothing has.
    /// </summary>
    public SceneRenderSystem Start(Func<PixelFormat, SceneRenderer, IFrameOverlay?>? createOverlay = null)
    {
        if (Renderer is not null)
        {
            throw new InvalidOperationException("The host has already started.");
        }

        if (_options.Scene is { } scene)
        {
            Scenes.Load(scene);
        }

        if (_options.Mood is null && SceneMood.Find(Game.World) is { } sceneMood)
        {
            Mood = Content.Load(sceneMood, RenderingJsonContext.Default.LightingMood);
        }

        int cameras = Game.World.CountEntities(in Cameras);
        if (cameras == 0)
        {
            LogNoCamera(_options.Scene ?? "The world");
        }

        createOverlay ??= static (_, _) => null;
        Renderer = new SceneRenderSystem(Window, Device, Game.World, CreateRenderer, _options.FrameLimit, createOverlay);
        return Renderer;
    }

    /// <summary>Adds the renderer after the frame systems added so far and runs the game until the window closes.</summary>
    public void Run()
    {
        SceneRenderSystem renderer = Renderer ?? Start();
        Game.FrameSystems.Add(renderer);
        Window.Run();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Renderer?.Dispose();
        Scenes.Dispose();
        Window.Dispose();
        Game.Dispose();
        Device.Dispose();
    }

    private SceneRenderer CreateRenderer(PixelFormat format)
    {
        return new SceneRenderer(Device, Content, format) { Mood = Mood };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Scene} has no camera, so the window stays empty.")]
    private partial void LogNoCamera(string scene);
}
