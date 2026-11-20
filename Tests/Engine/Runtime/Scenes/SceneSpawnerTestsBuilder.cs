using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Engine.Runtime.Tests.Scenes;

/// <summary>Builds the scene spawner under test with its registered components.</summary>
internal sealed class SceneSpawnerTestsBuilder
{
    private readonly List<Action<SceneSpawner>> _registrations = [];

    public SceneSpawnerTestsBuilder WithComponent<T>(string name, JsonTypeInfo<T> typeInfo)
        where T : struct
    {
        _registrations.Add(spawner => spawner.Register(name, typeInfo));
        return this;
    }

    public SceneSpawner Build()
    {
        SceneSpawner spawner = new();
        foreach (Action<SceneSpawner> register in _registrations)
        {
            register(spawner);
        }

        return spawner;
    }
}
