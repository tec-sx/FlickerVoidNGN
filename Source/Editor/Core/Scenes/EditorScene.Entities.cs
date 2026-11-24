using System.Globalization;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

namespace FlickerVoid.Editor.Core.Scenes;

public sealed partial class EditorScene
{
    private const string TransformName = "transform";

    /// <summary>Creates an entity in the open scene, saved with it, numbering a name another entity has; throws without an open scene.</summary>
    public Entity AddEntity(EntityData data)
    {
        if (ScenePath is null)
        {
            throw new InvalidOperationException("Open a scene before adding entities.");
        }

        if (data.Name.Length > 0 && Find(data.Name) is not null)
        {
            HashSet<string> names = GetNames();
            data.Name = GetFreeName(data.Name, names);
        }

        Entity entity = _loader.Add(ScenePath, data);
        EntityStep step = new($"Add {data.Name}", this, data, added: true);
        History.Record(step);
        EntitiesChanged?.Invoke();
        MarkEdited();
        return entity;
    }

    /// <summary>
    /// Adds a copy of the entity and of everything nested under it, each with every component it has and named after it with the
    /// next free number ("Lamp" and "Lamp 2" become "Lamp 3"), because changes to the scene file match entities by name. The copies
    /// nest as the originals do, the top one beside the original, and saveable copies get save ids no other entity has. Returns the top copy.
    /// </summary>
    public Entity DuplicateEntity(Entity entity)
    {
        List<Entity> tree = [entity, .. GetDescendants(entity)];
        HashSet<string> names = GetNames();
        HashSet<string> saveIds = GetSaveIds();
        Dictionary<string, string> copied = [];
        List<EntityData> copies = [];
        string verb = "Duplicate";
        foreach (Entity original in tree)
        {
            EntityData data = CaptureEntity(original);
            string name = data.Name;
            if (name.Length == 0)
            {
                name = "Entity";
            }

            if (copies.Count == 0)
            {
                verb = $"Duplicate {name}";
            }

            string free = GetFreeName(name, names);
            names.Add(free);
            copied.TryAdd(data.Name, free);
            data.Name = free;
            GiveFreeSaveId(data, saveIds);
            copies.Add(data);
        }

        for (int index = 1; index < copies.Count; index++)
        {
            EntityData copy = copies[index];
            if (copy.Parent is { } parent && copied.TryGetValue(parent, out string? parentCopy))
            {
                copy.Parent = parentCopy;
            }
        }

        History.BeginGroup(verb);
        Entity top = Entity.Null;
        foreach (EntityData copy in copies)
        {
            Entity added = AddEntity(copy);
            if (top == Entity.Null)
            {
                top = added;
            }
        }

        History.EndGroup();
        return top;
    }

    /// <summary>Destroys an entity and everything nested under it; the next save leaves them out of the scene file.</summary>
    public void DestroyEntity(Entity entity)
    {
        World world = Game.World;
        if (!world.IsAlive(entity))
        {
            return;
        }

        if (History.IsApplying)
        {
            Destroy(entity);
        }
        else
        {
            List<Entity> tree = [entity, .. GetDescendants(entity)];
            tree.Reverse();
            string name = world.TryGet(entity, out EntityName entityName) ? entityName.Value : "Entity";
            History.BeginGroup($"Delete {name}");
            foreach (Entity doomed in tree)
            {
                EntityData data = CaptureEntity(doomed);
                EntityStep step = new($"Delete {data.Name}", this, data, added: false);
                History.Record(step);
                Destroy(doomed);
            }

            History.EndGroup();
        }

        EntitiesChanged?.Invoke();
        MarkEdited();
    }

    /// <summary>
    /// Renames the entity, keeping what is nested under it there and rewriting the entity references that named it, as one undo step;
    /// false for an empty name or one another entity of the scene has, as names link entities.
    /// </summary>
    public bool RenameEntity(Entity entity, string name)
    {
        World world = Game.World;
        string wanted = name.Trim();
        if (!world.IsAlive(entity) || !world.TryGet(entity, out EntityName current) || wanted.Length == 0)
        {
            return false;
        }

        string before = current.Value;
        if (before == wanted)
        {
            return true;
        }

        if (Find(wanted) is not null)
        {
            return false;
        }

        string verb = $"Rename {before} to {wanted}";
        History.BeginGroup(verb);
        ApplyRename(before, wanted);
        RenameStep step = new(verb, this, before, wanted);
        History.Record(step);
        RenameReferences(before, wanted);
        History.EndGroup();
        return true;
    }

    /// <summary>The entity template the entity starts from in the open scene file, or null.</summary>
    public string? GetTemplate(Entity entity)
    {
        if (ScenePath is null || !Game.World.IsAlive(entity))
        {
            return null;
        }

        return _loader.GetTemplate(ScenePath, entity);
    }

    /// <summary>
    /// Makes the entity start from the entity template, or from none for null, as one undo step: it keeps every component and value
    /// it has, gains the template's components it lacks, and the scene file saves only what differs from the template.
    /// </summary>
    public void SetTemplate(Entity entity, string? template)
    {
        World world = Game.World;
        if (ScenePath is null || !world.IsAlive(entity) || !world.TryGet(entity, out EntityName entityName))
        {
            return;
        }

        string? before = GetTemplate(entity);
        if (before == template)
        {
            return;
        }

        string verb = $"Start {entityName.Value} from {template ?? "no template"}";
        History.BeginGroup(verb);
        ApplyTemplate(entityName.Value, template);
        TemplateStep step = new(verb, this, entityName.Value, before, template);
        History.Record(step);
        if (template is not null)
        {
            AddTemplateComponents(entity, template);
        }

        History.EndGroup();
    }

    /// <summary>
    /// Writes the entity's components, but its transform, as a new entity template at <paramref name="path"/> over the template it
    /// starts from, and makes the entity start from it; false when a file is there already.
    /// </summary>
    public bool SaveAsTemplate(Entity entity, string path)
    {
        World world = Game.World;
        string fullPath = Content.GetFullPath(path);
        if (ScenePath is null || !world.IsAlive(entity) || File.Exists(fullPath))
        {
            return false;
        }

        EntityData data = CaptureEntity(entity);
        data.Components.Remove(TransformName);
        EntityData reduced = _loader.Templates.Reduce(data);
        int depth = path.Count(static character => character == '/');
        string schema = string.Concat(Enumerable.Repeat("../", depth)) + ".schemas/entity.schema.json";
        EntityTemplateFile file = new() { Schema = schema, Base = data.Template, Components = reduced.Components };
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
        {
            JsonSerializer.Serialize(writer, file, RuntimeJsonContext.Default.EntityTemplateFile);
        }

        Content.Write(path, stream.ToArray());
        SetTemplate(entity, path);
        return true;
    }

    /// <summary>Makes the entity named <paramref name="name"/> start from the template, recording nothing.</summary>
    internal void ApplyTemplate(string name, string? template)
    {
        if (ScenePath is null || Find(name) is not { } entity)
        {
            return;
        }

        _loader.SetTemplate(ScenePath, entity, template);
        EntitiesChanged?.Invoke();
        MarkEdited();
    }

    /// <summary>Gives the entity, as undo steps, the template's components it lacks.</summary>
    private void AddTemplateComponents(Entity entity, string template)
    {
        World world = Game.World;
        EntityData expanded = _loader.Templates.Expand(new EntityData { Template = template });
        foreach (SceneComponent component in Game.Spawner.Components)
        {
            if (!component.Has(world, entity) && expanded.Components.TryGetValue(component.Name, out JsonElement value))
            {
                ReadComponent(entity, component, value);
            }
        }
    }

    /// <summary>Renames the entity named <paramref name="from"/>, nesting what was nested under it under the new name, recording nothing.</summary>
    internal void ApplyRename(string from, string to)
    {
        World world = Game.World;
        if (Find(from) is not { } renamed)
        {
            return;
        }

        IReadOnlyList<Entity> children = GetChildren(renamed);
        world.Set(renamed, new EntityName(to));
        foreach (Entity child in children)
        {
            world.Set(child, new EntityParent(to));
        }

        EntitiesChanged?.Invoke();
        MarkEdited();
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

        if (world.TryGet(entity, out EntityParent parent))
        {
            data.Parent = parent.Name;
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

    /// <summary>The names of the scene's entities.</summary>
    private HashSet<string> GetNames()
    {
        World world = Game.World;
        HashSet<string> names = [];
        foreach (Entity entity in Entities)
        {
            if (world.TryGet(entity, out EntityName entityName))
            {
                names.Add(entityName.Value);
            }
        }

        return names;
    }

    /// <summary>The save ids of the scene's saveable entities.</summary>
    private HashSet<string> GetSaveIds()
    {
        World world = Game.World;
        HashSet<string> ids = [];
        foreach (Entity entity in Entities)
        {
            if (world.TryGet(entity, out Saveable saveable) && saveable.Id.Length > 0)
            {
                ids.Add(saveable.Id);
            }
        }

        return ids;
    }

    /// <summary>Gives the copy's saveable component, when it has one with an id, an id none of <paramref name="taken"/> is, and takes it.</summary>
    private void GiveFreeSaveId(EntityData copy, HashSet<string> taken)
    {
        SceneComponent? component = FindComponent(typeof(Saveable));
        if (component is null || !copy.Components.TryGetValue(component.Name, out JsonElement json))
        {
            return;
        }

        Saveable saveable = json.Deserialize(RuntimeJsonContext.Default.Saveable);
        if (saveable.Id.Length == 0)
        {
            return;
        }

        saveable.Id = GetFreeName(saveable.Id, taken);
        taken.Add(saveable.Id);
        copy.Components[component.Name] = JsonSerializer.SerializeToElement(saveable, RuntimeJsonContext.Default.Saveable);
    }

    /// <summary>Rewrites every entity reference to <paramref name="from"/> in the scene's components to name <paramref name="to"/>, each an undo step.</summary>
    private void RenameReferences(string from, string to)
    {
        World world = Game.World;
        foreach (SceneComponent component in Game.Spawner.Components)
        {
            if (!EntityReferences.HasAny(component.TypeInfo))
            {
                continue;
            }

            foreach (Entity holder in Entities)
            {
                if (!component.Has(world, holder))
                {
                    continue;
                }

                JsonElement data = component.Write(world, holder);
                if (EntityReferences.Rename(component.TypeInfo, data, from, to) is { } renamed)
                {
                    ReadComponent(holder, component, renamed);
                }
            }
        }
    }

    private void Destroy(Entity entity)
    {
        Selection.Deselect(entity);
        Game.Spawner.Lifecycle.Destroy(Game.World, entity);
    }

    /// <summary>The name without a trailing number, numbered from 2 up until none of <paramref name="taken"/> is it.</summary>
    private static string GetFreeName(string name, HashSet<string> taken)
    {
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
