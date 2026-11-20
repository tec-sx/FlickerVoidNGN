using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>Spawns scenes with a JSON reader per registered component name, starting what it creates and stopping what it removes through its lifecycle.</summary>
public sealed class SceneSpawner : ISceneSpawner
{
    private readonly Dictionary<string, SceneComponent> _components = [];
    private readonly List<SceneComponent> _componentList = [];

    public SceneSpawner()
        : this(new EntityLifecycle())
    {
    }

    public SceneSpawner(EntityLifecycle lifecycle)
    {
        Lifecycle = lifecycle;
        ArrayRegistry.Add<EntityName>();
    }

    public EntityLifecycle Lifecycle { get; }

    public IReadOnlyList<SceneComponent> Components => _componentList;

    public void Register<T>(string name, JsonTypeInfo<T> typeInfo)
        where T : struct
    {
        SceneComponentOf<T> component = new(name, typeInfo);
        if (!_components.TryAdd(name, component))
        {
            throw new ArgumentException($"A component named '{name}' is already registered.", nameof(name));
        }

        _componentList.Add(component);

        // NativeAOT can't create Arch's component arrays through reflection, so every component type is registered up front.
        ArrayRegistry.Add<T>();
    }

    /// <summary>The schema of a scene file: its entities may have every registered component and no other.</summary>
    public JsonObject GetSchema()
    {
        JsonObject components = [];
        foreach (SceneComponent component in _componentList)
        {
            components[component.Name] = JsonSchemas.Create(component.TypeInfo);
        }

        JsonObject componentsSchema = new() { ["type"] = "object", ["properties"] = components, ["additionalProperties"] = false };
        JsonObject entityProperties = new() { ["name"] = new JsonObject { ["type"] = "string" }, ["components"] = componentsSchema };
        JsonObject entity = new() { ["type"] = "object", ["properties"] = entityProperties, ["additionalProperties"] = false };
        JsonObject entities = new() { ["type"] = "array", ["items"] = entity };
        JsonObject sceneProperties = new() { [JsonSchemas.SchemaProperty] = new JsonObject { ["type"] = "string" }, ["entities"] = entities };
        return new JsonObject { ["type"] = "object", ["properties"] = sceneProperties, ["additionalProperties"] = false };
    }

    public IReadOnlyList<Entity> Spawn(SceneData scene, World world)
    {
        List<Entity> spawned = new(scene.Entities.Count);
        try
        {
            foreach (EntityData data in scene.Entities)
            {
                Entity entity = Create(world, data);
                spawned.Add(entity);
            }

            foreach (Entity entity in spawned)
            {
                Lifecycle.Start(world, entity);
            }
        }
        catch
        {
            foreach (Entity entity in spawned)
            {
                Lifecycle.Destroy(world, entity);
            }

            throw;
        }

        return spawned;
    }

    public IReadOnlyList<Entity> Patch(SceneData previous, SceneData current, IReadOnlyList<Entity> entities, World world)
    {
        CheckComponentNames(current);
        bool[] kept = new bool[previous.Entities.Count];
        List<Entity> patched = new(current.Entities.Count);
        foreach (EntityData data in current.Entities)
        {
            int match = FindMatch(previous, kept, data);
            if (match >= 0 && world.IsAlive(entities[match]))
            {
                kept[match] = true;
                Entity entity = entities[match];
                PatchComponents(world, entity, previous.Entities[match], data);
                patched.Add(entity);
            }
            else
            {
                Entity entity = Create(world, data);
                Lifecycle.Start(world, entity);
                patched.Add(entity);
            }
        }

        for (int index = 0; index < kept.Length; index++)
        {
            if (!kept[index] && world.IsAlive(entities[index]))
            {
                Lifecycle.Destroy(world, entities[index]);
            }
        }

        return patched;
    }

    public SceneData Capture(SceneData previous, IReadOnlyList<Entity> entities, World world)
    {
        SceneData captured = new() { Schema = previous.Schema };
        for (int index = 0; index < entities.Count; index++)
        {
            Entity entity = entities[index];
            if (!world.IsAlive(entity))
            {
                continue;
            }

            EntityData data = CaptureEntity(world, entity, previous.Entities[index]);
            captured.Entities.Add(data);
        }

        return captured;
    }

    private EntityData CaptureEntity(World world, Entity entity, EntityData previous)
    {
        EntityData data = new();
        if (world.TryGet(entity, out EntityName name))
        {
            data.Name = name.Value;
        }

        foreach ((string key, JsonElement old) in previous.Components)
        {
            SceneComponent component = _components[key];
            if (!component.Has(world, entity))
            {
                continue;
            }

            JsonElement current = component.Write(world, entity);
            JsonElement rewritten = component.Rewrite(old);
            if (JsonElement.DeepEquals(current, rewritten))
            {
                data.Components.Add(key, old);
            }
            else
            {
                data.Components.Add(key, current);
            }
        }

        foreach (SceneComponent component in _componentList)
        {
            if (!data.Components.ContainsKey(component.Name) && component.Has(world, entity))
            {
                JsonElement current = component.Write(world, entity);
                data.Components.Add(component.Name, current);
            }
        }

        return data;
    }

    private static int FindMatch(SceneData previous, bool[] kept, EntityData data)
    {
        for (int index = 0; index < kept.Length; index++)
        {
            if (kept[index])
            {
                continue;
            }

            EntityData candidate = previous.Entities[index];
            if (candidate.Name != data.Name)
            {
                continue;
            }

            if (data.Name.Length > 0 || HasSameComponents(candidate, data))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool HasSameComponents(EntityData first, EntityData second)
    {
        if (first.Components.Count != second.Components.Count)
        {
            return false;
        }

        foreach ((string key, JsonElement value) in first.Components)
        {
            if (!second.Components.TryGetValue(key, out JsonElement other) || value.GetRawText() != other.GetRawText())
            {
                return false;
            }
        }

        return true;
    }

    public Entity Create(EntityData data, World world)
    {
        Entity entity = Create(world, data);
        Lifecycle.Start(world, entity);
        return entity;
    }

    private Entity Create(World world, EntityData data)
    {
        Entity entity = world.Create();
        try
        {
            if (data.Name.Length > 0)
            {
                EntityName name = new(data.Name);
                world.Add(entity, in name);
            }

            foreach ((string key, JsonElement value) in data.Components)
            {
                Write(world, entity, data, key, value);
            }
        }
        catch
        {
            world.Destroy(entity);
            throw;
        }

        return entity;
    }

    private void PatchComponents(World world, Entity entity, EntityData previous, EntityData current)
    {
        foreach ((string key, JsonElement value) in current.Components)
        {
            bool unchanged = previous.Components.TryGetValue(key, out JsonElement old) && old.GetRawText() == value.GetRawText();
            if (!unchanged)
            {
                Rewrite(world, entity, current, key, value);
            }
        }

        foreach (string key in previous.Components.Keys)
        {
            if (!current.Components.ContainsKey(key) && _components.TryGetValue(key, out SceneComponent? component))
            {
                Lifecycle.StopComponent(world, entity, component.TypeInfo.Type);
                component.Remove(world, entity);
            }
        }
    }

    /// <summary>Throws before anything changes when the scene names a component that isn't registered.</summary>
    private void CheckComponentNames(SceneData scene)
    {
        foreach (EntityData data in scene.Entities)
        {
            foreach (string key in data.Components.Keys)
            {
                if (!_components.ContainsKey(key))
                {
                    throw new InvalidDataException($"Entity '{data.Name}' has an unknown component '{key}'.");
                }
            }
        }
    }

    /// <summary>Writes a component of a started entity between its stop and start hooks, so they see the old and the new values.</summary>
    private void Rewrite(World world, Entity entity, EntityData data, string key, JsonElement value)
    {
        Type type = _components[key].TypeInfo.Type;
        Lifecycle.StopComponent(world, entity, type);
        Write(world, entity, data, key, value);
        Lifecycle.StartComponent(world, entity, type);
    }

    private void Write(World world, Entity entity, EntityData data, string key, JsonElement value)
    {
        if (!_components.TryGetValue(key, out SceneComponent? component))
        {
            throw new InvalidDataException($"Entity '{data.Name}' has an unknown component '{key}'.");
        }

        try
        {
            component.Read(world, entity, value);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new InvalidDataException($"Entity '{data.Name}' has an invalid '{key}' component: {exception.Message}", exception);
        }
    }
}
