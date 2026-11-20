using FlickerVoid.Engine.Editor.Selection;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Editor.Scenes;

/// <summary>
/// The world being edited and the content it is built from. The game in it never ticks: the editor shows and changes the world
/// as the scene file describes it, and Play runs the game in its own process.
/// </summary>
public sealed class EditorScene : IDisposable
{
    private readonly SceneLoader _loader;

    /// <summary>Opens an empty world over the content folder, or over the editor's own content without one.</summary>
    public EditorScene(ILoggerFactory loggerFactory, string? contentFolder)
    {
        Game = new Game(loggerFactory);
        Game.AddRendering();
        string folder = contentFolder ?? Path.Combine(AppContext.BaseDirectory, "Content");
        Content = new ContentManager(folder);
        ILogger<SceneLoader> logger = loggerFactory.CreateLogger<SceneLoader>();
        _loader = new SceneLoader(Game.Spawner, Content, Game.World, logger);
        Picker = new ScenePicker(Content);
    }

    public Game Game { get; }

    public IContentManager Content { get; }

    /// <summary>Finds the entity under a ray through the viewport.</summary>
    public ScenePicker Picker { get; }

    /// <summary>The entity the viewport and the inspector work on.</summary>
    public EditorSelection Selection { get; } = new();

    /// <summary>The open scene file, relative to the content folder, or null for an unsaved world.</summary>
    public string? ScenePath { get; private set; }

    /// <summary>Spawns a scene file from the content folder into the world.</summary>
    public void Open(string path)
    {
        _loader.Load(path);
        ScenePath = path;
    }

    public void Dispose()
    {
        Picker.Dispose();
        _loader.Dispose();
        Game.Dispose();
    }
}
