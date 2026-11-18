using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Arch.Core;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>Spawns a scene's entities into a world, reading each component from JSON by the name its type is registered under.</summary>
public sealed class SceneSpawner
{
    private readonly Dictionary<string, ComponentReader> _readers = [];

    public SceneSpawner()
    {
        ArrayRegistry.Add<EntityName>();
    }

    private delegate void ComponentReader(World world, Entity entity, JsonElement data);

    /// <summary>Lets scene files add components of type <typeparamref name="T"/> under <paramref name="name"/>, and registers the type with Arch.</summary>
    /// <remarks>NativeAOT can't create Arch's component arrays through reflection, so every component type is registered with <see cref="ArrayRegistry"/> up front.</remarks>
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

        ArrayRegistry.Add<T>();
    }

    /// <summary>Creates the scene's entities in order; if one fails, destroys those already created and throws.</summary>
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
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                throw new InvalidDataException($"Entity '{data.Name}' has an invalid '{key}' component: {exception.Message}", exception);
            }
        }
    }
}
