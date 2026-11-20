using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>Spawns scenes with a JSON reader per registered component name.</summary>
public sealed class SceneSpawner : ISceneSpawner
{
    private readonly Dictionary<string, SceneComponent> _components = [];
    private readonly List<SceneComponent> _componentList = [];

    public SceneSpawner()
    {
        ArrayRegistry.Add<EntityName>();
    }

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
        }
        catch
        {
            foreach (Entity entity in spawned)
            {
                world.Destroy(entity);
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
                patched.Add(entity);
            }
        }

        for (int index = 0; index < kept.Length; index++)
        {
            if (!kept[index] && world.IsAlive(entities[index]))
            {
                world.Destroy(entities[index]);
            }
        }

        return patched;
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
                Write(world, entity, current, key, value);
            }
        }

        foreach (string key in previous.Components.Keys)
        {
            if (!current.Components.ContainsKey(key) && _components.TryGetValue(key, out SceneComponent? component))
            {
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
