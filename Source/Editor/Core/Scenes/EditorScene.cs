using System.Globalization;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Editor.Core.Selection;
using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;

using Microsoft.Extensions.Logging;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>
/// The world being edited and the content it is built from. The game in it never ticks: the editor shows and changes the world
/// as the scene file describes it, and Play runs the game in its own process. Every edit goes through its methods, which raise
/// <see cref="Edited"/> and leave the scene unsaved until it is saved or another scene opens.
/// </summary>
public sealed class EditorScene : IDisposable
{
    private readonly SceneLoader _loader;
    private readonly ContentWatcher _watcher;

    /// <summary>Opens an empty world over the content.</summary>
    public EditorScene(ILoggerFactory loggerFactory, IContentManager content)
        : this(loggerFactory, content, [])
    {
    }

    /// <summary>Opens an empty world over the content, with the project's game modules added (see <see cref="Projects.GameCode"/>).</summary>
    public EditorScene(ILoggerFactory loggerFactory, IContentManager content, IReadOnlyList<IGameModule> modules)
    {
        Game = new Game(loggerFactory);
        Game.AddRendering();
        Game.AddFlow();
        foreach (IGameModule module in modules)
        {
            module.Add(Game);
        }

        Content = content;
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

    /// <summary>Creates an entity in the open scene, saved with it; throws without an open scene.</summary>
    public Entity AddEntity(EntityData data)
    {
        if (ScenePath is null)
        {
            throw new InvalidOperationException("Open a scene before adding entities.");
        }

        Entity entity = _loader.Add(ScenePath, data);
        EntitiesChanged?.Invoke();
        MarkEdited();
        return entity;
    }

    /// <summary>
    /// Adds a copy of the entity with every component it has, named after it with the next free number ("Lamp" and "Lamp 2"
    /// become "Lamp 3"), because changes to the scene file match entities by name.
    /// </summary>
    public Entity DuplicateEntity(Entity entity)
    {
        World world = Game.World;
        string name = "Entity";
        if (world.TryGet(entity, out EntityName entityName))
        {
            name = entityName.Value;
        }

        EntityData data = new() { Name = GetFreeName(name) };
        foreach (SceneComponent component in Game.Spawner.Components)
        {
            if (component.Has(world, entity))
            {
                JsonElement json = component.Write(world, entity);
                data.Components.Add(component.Name, json);
            }
        }

        return AddEntity(data);
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

        Game.Spawner.Lifecycle.Destroy(world, entity);
        EntitiesChanged?.Invoke();
        MarkEdited();
    }

    /// <summary>Gives the entity the component, replacing the one it has.</summary>
    public void SetComponent<T>(Entity entity, in T component)
        where T : struct
    {
        World world = Game.World;
        if (!world.IsAlive(entity))
        {
            return;
        }

        EntityLifecycle lifecycle = Game.Spawner.Lifecycle;
        lifecycle.StopComponent(world, entity, typeof(T));
        world.Set(entity, component);
        lifecycle.StartComponent(world, entity, typeof(T));
        MarkEdited();
    }

    /// <summary>Gives the entity the component registered for scene files, from a boxed value of its type.</summary>
    public void SetComponent(Entity entity, SceneComponent component, object value)
    {
        World world = Game.World;
        if (!world.IsAlive(entity))
        {
            return;
        }

        EntityLifecycle lifecycle = Game.Spawner.Lifecycle;
        Type type = component.TypeInfo.Type;
        lifecycle.StopComponent(world, entity, type);
        component.Set(world, entity, value);
        lifecycle.StartComponent(world, entity, type);
        MarkEdited();
    }

    /// <summary>Gives the entity the component with its defaults, unless it has it already.</summary>
    public void AddComponent(Entity entity, SceneComponent component)
    {
        World world = Game.World;
        if (!world.IsAlive(entity) || component.Has(world, entity))
        {
            return;
        }

        using JsonDocument defaults = JsonDocument.Parse("{}");
        component.Read(world, entity, defaults.RootElement);
        Type type = component.TypeInfo.Type;
        Game.Spawner.Lifecycle.StartComponent(world, entity, type);
        MarkEdited();
    }

    public void RemoveComponent(Entity entity, SceneComponent component)
    {
        World world = Game.World;
        if (!world.IsAlive(entity) || !component.Has(world, entity))
        {
            return;
        }

        Type type = component.TypeInfo.Type;
        Game.Spawner.Lifecycle.StopComponent(world, entity, type);
        component.Remove(world, entity);
        MarkEdited();
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

    public void Dispose()
    {
        _watcher.Dispose();
        Picker.Dispose();
        _loader.Dispose();
        Game.Dispose();
    }

    private void MarkEdited()
    {
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

    /// <summary>The name without a trailing number, numbered from 2 up until no entity of the scene has it.</summary>
    private string GetFreeName(string name)
    {
        World world = Game.World;
        HashSet<string> taken = [];
        foreach (Entity entity in Entities)
        {
            if (world.TryGet(entity, out EntityName entityName))
            {
                taken.Add(entityName.Value);
            }
        }

        string baseName = name;
        int space = name.LastIndexOf(' ');
        if (space > 0 && int.TryParse(name.AsSpan(space + 1), NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            baseName = name[..space];
        }

        int number = 2;
        string free = $"{baseName} {number}";
        while (taken.Contains(free))
        {
            number++;
            free = $"{baseName} {number}";
        }

        return free;
    }
}
