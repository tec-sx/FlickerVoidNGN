using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>Spawns scenes with a JSON reader per registered component name.</summary>
public sealed class SceneSpawner : ISceneSpawner
{
    private readonly Dictionary<string, ComponentReader> _readers = [];

    private delegate void ComponentReader(World world, Entity entity, JsonElement data);

    public void Register<T>(string name, JsonTypeInfo<T> typeInfo)
        where T : struct
    {
        void Read(World world, Entity entity, JsonElement data)
        {
            T component = data.Deserialize(typeInfo);
            world.Add(entity, in component);
        }

        if (!_readers.TryAdd(name, Read))
        {
            throw new ArgumentException($"A component named '{name}' is already registered.", nameof(name));
        }
    }

    public IReadOnlyList<Entity> Spawn(SceneData scene, World world)
    {
        List<Entity> spawned = new(scene.Entities.Count);
        try
        {
            foreach (EntityData data in scene.Entities)
            {
                Entity entity = world.Create();
                spawned.Add(entity);
                AddComponents(world, entity, data);
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

    private void AddComponents(World world, Entity entity, EntityData data)
    {
        if (data.Name.Length > 0)
        {
            EntityName name = new(data.Name);
            world.Add(entity, in name);
        }

        foreach ((string key, JsonElement value) in data.Components)
        {
            if (!_readers.TryGetValue(key, out ComponentReader? read))
            {
                throw new InvalidDataException($"Entity '{data.Name}' has an unknown component '{key}'.");
            }

            try
            {
                read(world, entity, value);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Entity '{data.Name}' has an invalid '{key}' component: {exception.Message}", exception);
            }
        }
    }
}
