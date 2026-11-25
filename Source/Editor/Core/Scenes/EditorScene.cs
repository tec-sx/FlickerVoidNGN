using Arch.Core;

using FlickerVoid.Editor.Core.Selection;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Settings;
using FlickerVoid.Engine.Vehicles;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>
/// The world being edited and the content it is built from. The game in it never ticks: the editor shows and changes the world
/// as the scene file describes it, and Play runs the game in its own process. Every edit goes through its methods, which raise
/// <see cref="Edited"/> and leave the scene unsaved until it is saved or another scene opens.
/// </summary>
public sealed partial class EditorScene : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<EditorScene> _logger;
    private SceneLoader _loader;
    private ContentWatcher _watcher;

    /// <summary>Opens an empty world over the content.</summary>
    public EditorScene(ILoggerFactory loggerFactory, IContentManager content)
        : this(loggerFactory, content, [])
    {
    }

    /// <summary>Opens an empty world over the content, with the project's game modules added (see <see cref="Projects.GameCode"/>).</summary>
    public EditorScene(ILoggerFactory loggerFactory, IContentManager content, IReadOnlyList<IGameModule> modules)
    {
        _loggerFactory = loggerFactory;
        Content = content;
        _logger = loggerFactory.CreateLogger<EditorScene>();
        Game = CreateGame(loggerFactory, content, modules);
        LoadSettings();
        Content.Changed += HandleContentChanged;
        Picker = new ScenePicker(Content) { Meshes = Meshes };
        _loader = CreateLoader(Game);
        _watcher = CreateWatcher();
    }

    /// <summary>The game the world is in; <see cref="Reload"/> replaces it, so read it each time rather than keeping it.</summary>
    public Game Game { get; private set; }

    public IContentManager Content { get; }

    /// <summary>Finds the entity under a ray through the viewport.</summary>
    public ScenePicker Picker { get; }

    /// <summary>Turns the world's mesh names into models with the sources the game's modules added; replaced with the game.</summary>
    public MeshSources Meshes => Game.Services.GetService<MeshSources>()!;

    /// <summary>The entity the viewport and the inspector work on.</summary>
    public EditorSelection Selection { get; } = new();

    /// <summary>The scene's undo and redo steps, recorded by its edit methods and cleared when another scene opens.</summary>
    public UndoHistory History { get; } = new();

    /// <summary>Raised after a scene file was opened.</summary>
    public event Action? Opened;

    /// <summary>Raised when the project's settings file changed on disk and the game's console variables were set from it again.</summary>
    public event Action? SettingsChanged;

    /// <summary>Raised after the editor added or destroyed an entity, or opened a scene.</summary>
    public event Action? EntitiesChanged;

    /// <summary>Raised after each edit of the world: a component set, added or removed, an entity added or destroyed.</summary>
    public event Action? Edited;

    /// <summary>Raised when the scene gets edits that are not saved, and when it loses them by saving or opening a scene.</summary>
    public event Action? UnsavedChanged;

    /// <summary>Whether the world holds edits that the open scene file doesn't have yet.</summary>
    public bool IsUnsaved { get; private set; }

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
        Game.Hierarchy.Update(Game.World);
        History.Clear();
        SetUnsaved(false);
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
        Game.Hierarchy.Update(Game.World);
    }

    /// <summary>Writes the world into the open scene file; false when the world has no scene file yet.</summary>
    public bool Save()
    {
        if (ScenePath is null)
        {
            return false;
        }

        _loader.Save(ScenePath);
        SetUnsaved(false);
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

    /// <summary>
    /// Replaces the game with one made with <paramref name="modules"/>, the project's code as built again, and spawns the open
    /// scene into it as the world has it now, so edits that are not saved survive; the selection follows its entities by name.
    /// Undo steps hold the old code's components, so they are forgotten. When the new game can't be made or can't spawn the
    /// scene, such as when a component it uses is gone, it throws and the old game stays.
    /// </summary>
    public void Reload(IReadOnlyList<IGameModule> modules)
    {
        string? path = ScenePath;
        byte[]? captured = Capture();
        Game game = CreateGame(_loggerFactory, Content, modules);
        SceneLoader loader = CreateLoader(game);
        try
        {
            if (path is not null && captured is not null)
            {
                loader.Load(path, captured);
            }
        }
        catch
        {
            loader.Dispose();
            game.Dispose();
            throw;
        }

        List<string> selected = GetSelectedNames(out string? primary);
        Selection.Select(null);
        Stop();
        Game = game;
        LoadSettings();
        Picker.Meshes = Meshes;
        _loader = loader;
        _watcher = CreateWatcher();
        History.Clear();
        Select(selected, primary);
        Opened?.Invoke();
        EntitiesChanged?.Invoke();
    }

    public void Dispose()
    {
        Content.Changed -= HandleContentChanged;
        Stop();
        Picker.Dispose();
    }

    /// <summary>A game over the content with what the editor shows (rendering, flows, input and vehicles), then the project's modules.</summary>
    private static Game CreateGame(ILoggerFactory loggerFactory, IContentManager content, IReadOnlyList<IGameModule> modules)
    {
        Game game = new(loggerFactory);
        try
        {
            game.Services.AddService(content);
            game.AddRendering();
            game.AddFlow();
            game.AddInput();
            game.AddVehicles();
            foreach (IGameModule module in modules)
            {
                module.Add(game);
            }
        }
        catch
        {
            game.Dispose();
            throw;
        }

        return game;
    }

    /// <summary>Sets the game's console variables as the project's settings file says, logging what it skipped.</summary>
    private void LoadSettings()
    {
        string path = Content.GetFullPath(SettingsFile.FileName);
        IReadOnlyList<string> problems = SettingsFile.Load(path, Game.DebugConsole);
        foreach (string problem in problems)
        {
            LogSettingsProblem(path, problem);
        }
    }

    private void HandleContentChanged(string fullPath)
    {
        string settings = Content.GetFullPath(SettingsFile.FileName);
        if (!ContentManager.PathComparer.Equals(fullPath, settings))
        {
            return;
        }

        LoadSettings();
        SettingsChanged?.Invoke();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Path}: {Problem}")]
    private partial void LogSettingsProblem(string path, string problem);

    private SceneLoader CreateLoader(Game game)
    {
        ILogger<SceneLoader> logger = _loggerFactory.CreateLogger<SceneLoader>();
        return new SceneLoader(game.Spawner, Content, game.World, logger);
    }

    private ContentWatcher CreateWatcher()
    {
        ContentWatcher watcher = new(Content, Game.Schemas);
        watcher.Initialize();
        return watcher;
    }

    private List<string> GetSelectedNames(out string? primary)
    {
        World world = Game.World;
        List<string> names = [];
        primary = null;
        foreach (Entity entity in Selection.Entities)
        {
            if (world.IsAlive(entity) && world.TryGet(entity, out EntityName name))
            {
                names.Add(name.Value);
            }
        }

        if (Selection.Selected is { } selected && world.IsAlive(selected) && world.TryGet(selected, out EntityName selectedName))
        {
            primary = selectedName.Value;
        }

        return names;
    }

    private void Stop()
    {
        _watcher.Dispose();
        _loader.Dispose();
        Game.Dispose();
    }

    private void Select(List<string> names, string? primary)
    {
        List<Entity> entities = [];
        Entity? primaryEntity = null;
        foreach (string name in names)
        {
            if (Find(name) is not { } entity)
            {
                continue;
            }

            entities.Add(entity);
            if (name == primary)
            {
                primaryEntity = entity;
            }
        }

        if (entities.Count > 0)
        {
            Selection.SelectAll(entities, primaryEntity);
        }
    }

    /// <summary>The first live entity of the open scene with the name, or null.</summary>
    public Entity? Find(string name)
    {
        World world = Game.World;
        foreach (Entity entity in Entities)
        {
            if (world.TryGet(entity, out EntityName entityName) && entityName.Value == name)
            {
                return entity;
            }
        }

        return null;
    }

    /// <summary>Moves nested entities after their parents, so edits of a parent carry its children along.</summary>
    private void MarkEdited()
    {
        Game.Hierarchy.Update(Game.World);
        Edited?.Invoke();
        SetUnsaved(true);
    }

    private void SetUnsaved(bool unsaved)
    {
        if (IsUnsaved == unsaved)
        {
            return;
        }

        IsUnsaved = unsaved;
        UnsavedChanged?.Invoke();
    }
}
