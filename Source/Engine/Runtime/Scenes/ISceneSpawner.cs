using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using Arch.Core;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>Spawns a scene's entities into a world, reading each component from JSON by the name its type is registered under.</summary>
public interface ISceneSpawner
{
    /// <summary>The components scene files may add, in the order they were registered.</summary>
    IReadOnlyList<SceneComponent> Components { get; }

    /// <summary>Lets scene files add components of type <typeparamref name="T"/> under <paramref name="name"/>, and registers the type with Arch.</summary>
    void Register<T>(string name, JsonTypeInfo<T> typeInfo)
        where T : struct;

    /// <summary>The JSON Schema of a scene file, with every registered component.</summary>
    JsonObject GetSchema();

    /// <summary>Creates the scene's entities in order; if one fails, destroys those already created and throws.</summary>
    IReadOnlyList<Entity> Spawn(SceneData scene, World world);

    /// <summary>Creates one entity from its scene data; if a component fails, destroys the entity and throws.</summary>
    Entity Create(EntityData data, World world);

    /// <summary>
    /// Brings entities spawned from <paramref name="previous"/> in line with <paramref name="current"/>, a newer version of the
    /// same file: entities match by name (unnamed ones by identical data), only components whose data changed are set, and
    /// entities the file added or removed are created or destroyed. Returns the entities in the order of <paramref name="current"/>.
    /// </summary>
    IReadOnlyList<Entity> Patch(SceneData previous, SceneData current, IReadOnlyList<Entity> entities, World world);

    /// <summary>
    /// The scene file for entities spawned from <paramref name="previous"/> as the world has them now, entity by entity in the
    /// same order: components whose values the world kept stay as the file wrote them, changed or added ones are written in full,
    /// removed ones are left out, and destroyed entities are dropped.
    /// </summary>
    SceneData Capture(SceneData previous, IReadOnlyList<Entity> entities, World world);
}
