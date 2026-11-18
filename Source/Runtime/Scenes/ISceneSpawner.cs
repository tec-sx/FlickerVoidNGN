using System.Text.Json.Serialization.Metadata;

using Arch.Core;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>Spawns a scene's entities into a world, reading each component from JSON by the name its type is registered under.</summary>
public interface ISceneSpawner
{
    /// <summary>Lets scene files add components of type <typeparamref name="T"/> under <paramref name="name"/>, and registers the type with Arch.</summary>
    void Register<T>(string name, JsonTypeInfo<T> typeInfo)
        where T : struct;

    /// <summary>Creates the scene's entities in order; if one fails, destroys those already created and throws.</summary>
    IReadOnlyList<Entity> Spawn(SceneData scene, World world);
}
