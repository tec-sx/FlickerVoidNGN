using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Tests.Scenes;

public sealed class SceneSpawnerTests : IDisposable
{
    private readonly World _world = World.Create();
    private readonly SceneSpawner _spawner = new();

    public SceneSpawnerTests()
    {
        _spawner.Register("transform", RuntimeJsonContext.Default.Transform);
    }

    public void Dispose()
    {
        World.Destroy(_world);
    }

    [Fact]
    public void SpawnsNamedEntitiesWithTheirComponents()
    {
        SceneData scene = Parse("""
            {
              "entities": [
                { "name": "Crate", "components": { "transform": { "position": [1, 5, 0] } } },
                { "name": "Floor" }
              ]
            }
            """);

        IReadOnlyList<Entity> entities = _spawner.Spawn(scene, _world);

        Assert.Equal(2, entities.Count);
        EntityName crateName = _world.Get<EntityName>(entities[0]);
        Transform crateTransform = _world.Get<Transform>(entities[0]);
        EntityName floorName = _world.Get<EntityName>(entities[1]);
        bool floorHasTransform = _world.Has<Transform>(entities[1]);
        Vector3 cratePosition = new(1, 5, 0);
        Assert.Equal("Crate", crateName.Value);
        Assert.Equal(cratePosition, crateTransform.Position);
        Assert.Equal(Vector3.One, crateTransform.Scale);
        Assert.Equal("Floor", floorName.Value);
        Assert.False(floorHasTransform);
    }

    [Fact]
    public void LeavesUnnamedEntitiesWithoutAName()
    {
        SceneData scene = Parse("""{ "entities": [ {} ] }""");

        IReadOnlyList<Entity> entities = _spawner.Spawn(scene, _world);

        bool hasName = _world.Has<EntityName>(entities[0]);
        Assert.False(hasName);
    }

    [Fact]
    public void UnknownComponentThrowsAndSpawnsNothing()
    {
        SceneData scene = Parse("""
            {
              "entities": [
                { "name": "Crate", "components": { "transform": {} } },
                { "name": "Lamp", "components": { "trasform": {} } }
              ]
            }
            """);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => _spawner.Spawn(scene, _world));

        Assert.Contains("'Lamp'", exception.Message);
        Assert.Contains("'trasform'", exception.Message);
        Assert.Equal(0, _world.Size);
    }

    [Fact]
    public void InvalidComponentDataNamesTheEntityAndComponent()
    {
        SceneData scene = Parse("""{ "entities": [ { "name": "Crate", "components": { "transform": { "position": [1, 2] } } } ] }""");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => _spawner.Spawn(scene, _world));

        Assert.Contains("'Crate'", exception.Message);
        Assert.Contains("'transform'", exception.Message);
        Assert.Equal(0, _world.Size);
    }

    [Fact]
    public void RegisteringANameTwiceThrows()
    {
        Assert.Throws<ArgumentException>(() => _spawner.Register("transform", RuntimeJsonContext.Default.Transform));
    }

    private static SceneData Parse(string json)
    {
        SceneData? scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData);
        Assert.NotNull(scene);
        return scene;
    }
}
