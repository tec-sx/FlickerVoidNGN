using Arch.Core;

using FlickerVoid.Engine.Editor.Selection;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;

using Microsoft.Extensions.Logging;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

namespace FlickerVoid.Engine.Editor.Scenes;

/// <summary>
/// The world being edited and the content it is built from. The game in it never ticks: the editor shows and changes the world
/// as the scene file describes it, and Play runs the game in its own process.
/// </summary>
public sealed class EditorScene : IDisposable
{
    private readonly SceneLoader _loader;
    private readonly ContentWatcher _watcher;

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
        _watcher = new ContentWatcher(Content, Game.Schemas);
        _watcher.Initialize();
    }

    public Game Game { get; }

    public IContentManager Content { get; }

    /// <summary>Finds the entity under a ray through the viewport.</summary>
    public ScenePicker Picker { get; }

    /// <summary>The entity the viewport and the inspector work on.</summary>
    public EditorSelection Selection { get; } = new();

    /// <summary>Raised after a scene file was opened.</summary>
    public event Action? Opened;

    /// <summary>Raised after the editor added or destroyed an entity, or opened a scene.</summary>
    public event Action? EntitiesChanged;

    /// <summary>The live entities of the open scene, in the file's order; empty without one.</summary>
    public IReadOnlyList<Entity> Entities
    {
        get
        {
            if (ScenePath is null)
            {
                return [];
            }

            World world = Game.World;
            return [.. _loader.GetEntities(ScenePath).Where(world.IsAlive)];
        }
    }

    /// <summary>Creates an entity in the open scene, saved with it; throws without an open scene.</summary>
    public Entity AddEntity(EntityData data)
    {
        if (ScenePath is null)
        {
            throw new InvalidOperationException("Open a scene before adding entities.");
        }

        Entity entity = _loader.Add(ScenePath, data);
        EntitiesChanged?.Invoke();
        return entity;
    }

    /// <summary>Destroys an entity; the next save leaves it out of the scene file.</summary>
    public void DestroyEntity(Entity entity)
    {
        World world = Game.World;
        if (!world.IsAlive(entity))
        {
            return;
        }

        if (Selection.Selected == entity)
        {
            Selection.Select(null);
        }

        world.Destroy(entity);
        EntitiesChanged?.Invoke();
    }

    /// <summary>The open scene file, relative to the content folder, or null for an unsaved world.</summary>
    public string? ScenePath { get; private set; }

    /// <summary>Spawns a scene file from the content folder into the world, in place of the scene open before.</summary>
    public void Open(string path)
    {
        Selection.Select(null);
        string? previous = ScenePath;
        if (previous is not null && string.Equals(Content.GetFullPath(previous), Content.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
        {
            _loader.Unload(previous);
            ScenePath = null;
            previous = null;
        }

        // Loads first, so a scene that can't be read leaves the open one in place.
        _loader.Load(path);
        if (previous is not null)
        {
            _loader.Unload(previous);
        }

        ScenePath = path;
        Opened?.Invoke();
        EntitiesChanged?.Invoke();
    }

    /// <summary>
    /// Reports content files that changed on disk, so caches reload them; in a development build the watcher also keeps the
    /// builder running over the source folders, so source edits made outside the editor arrive too. Call it often.
    /// </summary>
    public void Poll()
    {
        GameTime time = default;
        _watcher.Update(in time);
    }

    /// <summary>Writes the world into the open scene file; false when the world has no scene file yet.</summary>
    public bool Save()
    {
        if (ScenePath is null)
        {
            return false;
        }

        _loader.Save(ScenePath);
        return true;
    }

    /// <summary>The open scene file's contents as the world has its entities now, as <see cref="Save"/> would write them; null when the world has no scene file.</summary>
    public byte[]? Capture()
    {
        if (ScenePath is null)
        {
            return null;
        }

        return _loader.Capture(ScenePath);
    }

    public void Dispose()
    {
        _watcher.Dispose();
        Picker.Dispose();
        _loader.Dispose();
        Game.Dispose();
    }
}
