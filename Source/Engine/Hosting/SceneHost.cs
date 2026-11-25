using Arch.Core;

using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
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
        Game.Services.AddService<IContentManager>(Content);
        Game.AddRendering();
        Window = new GameWindow(Game, title, 1280, 720);
        Game.AddInput();
        Input = Game.AddInputActions(Window.Input);
        ILogger<SceneLoader> sceneLogger = loggerFactory.CreateLogger<SceneLoader>();
        Scenes = new SceneLoader(Game.Spawner, Content, Game.World, sceneLogger);
        Game.Services.AddService(Scenes.Templates);
        Data = new DataAssets(Content, Game.Data);
        Game.Services.AddService(Data);
        Game.AddFlow();
        Flows = new FlowGraphs(Content, Game.Data);
        Game.Services.AddService(Flows);
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string saveFolder = options.SaveFolder ?? Path.Combine(localData, "FlickerVoid", title, "Saves");
        SaveSettings saveSettings = new() { Folder = saveFolder };
        Saves = Game.AddSaves(saveSettings);
        Travel = Game.AddSceneTravel(Scenes);
        ContentWatcher watcher = new(Content, Game.Schemas);
        Game.FrameSystems.Add(watcher);
        Moods = Game.AddMoods(Content);
        Moods.SceneMoodPath = options.Mood;
    }

    public IGraphicsDevice Device { get; }

    public Game Game { get; }

    public GameWindow Window { get; }

    public ContentManager Content { get; }

    public SceneLoader Scenes { get; }

    /// <summary>Reads the game's data asset and table files.</summary>
    public DataAssets Data { get; }

    /// <summary>The input actions systems read, from the map the game sets.</summary>
    public InputActions Input { get; }

    /// <summary>Reads the game's flow graphs and starts runs of them.</summary>
    public FlowGraphs Flows { get; }

    /// <summary>Writes the game to save slots and reads it back.</summary>
    public SaveSystem Saves { get; }

    /// <summary>Moves the game from scene to scene; <see cref="Start"/> travels to the options' scene.</summary>
    public SceneTravel Travel { get; }

    /// <summary>
    /// Keeps the mood the renderer draws with each frame: the scene's environment and mood (the options' mood file in place of
    /// the one the scene names), the story's and the live shot's over them.
    /// </summary>
    public MoodSystem Moods { get; }

    /// <summary>The render system, once <see cref="Start"/> has created it.</summary>
    public SceneRenderSystem? Renderer { get; private set; }

    /// <summary>
    /// Travels to the options' scene, starts play (activating its entities) and creates the renderer, with an overlay when <paramref name="createOverlay"/> makes one;
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
            Travel.Travel(scene);
        }

        Game.Lifecycle.Play(Game.World);

        GameTime beforeTheFirstFrame = default;
        Moods.Update(in beforeTheFirstFrame);

        int cameras = Game.World.CountEntities(in Cameras);
        if (cameras == 0)
        {
            LogNoCamera(_options.Scene ?? "The world");
        }

        createOverlay ??= static (_, _) => null;
        Renderer = new SceneRenderSystem(Window, Device, Game.World, CreateRenderer, _options.FrameLimit, createOverlay) { MoodSource = () => Moods.Current };
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
        Data.Dispose();
        Window.Dispose();
        Game.Dispose();
        Device.Dispose();
    }

    private SceneRenderer CreateRenderer(PixelFormat format)
    {
        SceneRenderer renderer = new(Device, Content, format) { Mood = Moods.Current, Sky = true, RayTracing = true };
        SceneRendererSetup? setup = Game.Services.GetService<SceneRendererSetup>();
        setup?.Apply(renderer);
        return renderer;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Scene} has no camera, so the window stays empty.")]
    private partial void LogNoCamera(string scene);
}
