using System.Globalization;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Editor.Core.Selection;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Platform.Input;
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
        Game.AddInput();
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

    /// <summary>The scene's undo and redo steps, recorded by its edit methods and cleared when another scene opens.</summary>
    public UndoHistory History { get; } = new();

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
        EntityStep step = new($"Add {data.Name}", this, data, added: true);
        History.Record(step);
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
        EntityData data = CaptureEntity(entity);
        string name = data.Name;
        if (name.Length == 0)
        {
            name = "Entity";
        }

        data.Name = GetFreeName(name);
        History.BeginGroup($"Duplicate {name}");
        Entity copy = AddEntity(data);
        History.EndGroup();
        return copy;
    }

    /// <summary>Destroys an entity; the next save leaves it out of the scene file.</summary>
    public void DestroyEntity(Entity entity)
    {
        World world = Game.World;
        if (!world.IsAlive(entity))
        {
            return;
        }

        Selection.Deselect(entity);
        if (!History.IsApplying)
        {
            EntityData data = CaptureEntity(entity);
            EntityStep step = new($"Delete {data.Name}", this, data, added: false);
            History.Record(step);
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

        SceneComponent? registered = FindComponent(typeof(T));
        JsonElement? before = WriteComponent(entity, registered);
        EntityLifecycle lifecycle = Game.Spawner.Lifecycle;
        lifecycle.StopComponent(world, entity, typeof(T));
        world.Set(entity, component);
        lifecycle.StartComponent(world, entity, typeof(T));
        if (registered is not null)
        {
            RecordComponent("Edit", entity, registered, before);
        }

        MarkEdited();
    }

    /// <summary>Files the entity under an outline folder, such as <c>Lights/Hall</c>, or at the top for null or empty.</summary>
    public void SetFolder(Entity entity, string? path)
    {
        World world = Game.World;
        if (!world.IsAlive(entity))
        {
            return;
        }

        string? current = world.TryGet(entity, out EntityFolder folder) ? folder.Path : null;
        string? wanted = string.IsNullOrEmpty(path) ? null : path;
        if (current == wanted)
        {
            return;
        }

        if (!History.IsApplying && world.TryGet(entity, out EntityName entityName))
        {
            FolderStep step = new($"Move {entityName.Value} to folder", this, entityName.Value, current, wanted);
            History.Record(step);
        }

        if (wanted is null)
        {
            world.Remove<EntityFolder>(entity);
        }
        else if (current is null)
        {
            world.Add(entity, new EntityFolder(wanted));
        }
        else
        {
            world.Set(entity, new EntityFolder(wanted));
        }

        EntitiesChanged?.Invoke();
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

        JsonElement? before = WriteComponent(entity, component);
        EntityLifecycle lifecycle = Game.Spawner.Lifecycle;
        Type type = component.TypeInfo.Type;
        lifecycle.StopComponent(world, entity, type);
        component.Set(world, entity, value);
        lifecycle.StartComponent(world, entity, type);
        RecordComponent("Edit", entity, component, before);
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
        RecordComponent("Add", entity, component, null);
        MarkEdited();
    }

    /// <summary>Gives the entity the component as the JSON describes it, replacing the one it has.</summary>
    public void ReadComponent(Entity entity, SceneComponent component, JsonElement data)
    {
        World world = Game.World;
        if (!world.IsAlive(entity))
        {
            return;
        }

        JsonElement? before = WriteComponent(entity, component);
        EntityLifecycle lifecycle = Game.Spawner.Lifecycle;
        Type type = component.TypeInfo.Type;
        if (before is not null)
        {
            lifecycle.StopComponent(world, entity, type);
            component.Remove(world, entity);
        }

        component.Read(world, entity, data);
        lifecycle.StartComponent(world, entity, type);
        RecordComponent("Edit", entity, component, before);
        MarkEdited();
    }

    public void RemoveComponent(Entity entity, SceneComponent component)
    {
        World world = Game.World;
        if (!world.IsAlive(entity) || !component.Has(world, entity))
        {
            return;
        }

        JsonElement? before = WriteComponent(entity, component);
        Type type = component.TypeInfo.Type;
        Game.Spawner.Lifecycle.StopComponent(world, entity, type);
        component.Remove(world, entity);
        RecordComponent("Remove", entity, component, before);
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

    private SceneComponent? FindComponent(Type type)
    {
        foreach (SceneComponent component in Game.Spawner.Components)
        {
            if (component.TypeInfo.Type == type)
            {
                return component;
            }
        }

        return null;
    }

    private JsonElement? WriteComponent(Entity entity, SceneComponent? component)
    {
        if (History.IsApplying || component is null || !component.Has(Game.World, entity))
        {
            return null;
        }

        return component.Write(Game.World, entity);
    }

    private void RecordComponent(string verb, Entity entity, SceneComponent component, JsonElement? before)
    {
        World world = Game.World;
        if (History.IsApplying || !world.TryGet(entity, out EntityName entityName))
        {
            return;
        }

        JsonElement? after = null;
        if (component.Has(world, entity))
        {
            after = component.Write(world, entity);
        }

        ComponentStep step = new($"{verb} {entityName.Value}", this, entityName.Value, component, before, after);
        History.Record(step);
    }

    /// <summary>The entity as a scene file would hold it in full: name, template, folder and every component.</summary>
    private EntityData CaptureEntity(Entity entity)
    {
        World world = Game.World;
        EntityData data = new();
        if (world.TryGet(entity, out EntityName entityName))
        {
            data.Name = entityName.Value;
        }

        if (ScenePath is not null)
        {
            data.Template = _loader.GetTemplate(ScenePath, entity);
        }

        if (world.TryGet(entity, out EntityFolder folder))
        {
            data.Folder = folder.Path;
        }

        foreach (SceneComponent component in Game.Spawner.Components)
        {
            if (component.Has(world, entity))
            {
                JsonElement json = component.Write(world, entity);
                data.Components.Add(component.Name, json);
            }
        }

        return data;
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
