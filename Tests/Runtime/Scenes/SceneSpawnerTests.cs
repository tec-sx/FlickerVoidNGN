using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Tests.Scenes;

public sealed class SceneSpawnerTests : IDisposable
{
    private readonly World _world = World.Create();
    private readonly SceneSpawner _spawner = new SceneSpawnerTestsBuilder()
        .WithComponent("transform", RuntimeJsonContext.Default.Transform)
        .Build();

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

        entities.Should().HaveCount(2);
        EntityName crateName = _world.Get<EntityName>(entities[0]);
        Transform crateTransform = _world.Get<Transform>(entities[0]);
        EntityName floorName = _world.Get<EntityName>(entities[1]);
        bool floorHasTransform = _world.Has<Transform>(entities[1]);
        Vector3 cratePosition = new(1, 5, 0);
        crateName.Value.Should().Be("Crate");
        crateTransform.Position.Should().Be(cratePosition);
        crateTransform.Scale.Should().Be(Vector3.One);
        floorName.Value.Should().Be("Floor");
        floorHasTransform.Should().BeFalse();
    }

    [Fact]
    public void LeavesUnnamedEntitiesWithoutAName()
    {
        SceneData scene = Parse("""{ "entities": [ {} ] }""");

        IReadOnlyList<Entity> entities = _spawner.Spawn(scene, _world);

        bool hasName = _world.Has<EntityName>(entities[0]);
        hasName.Should().BeFalse();
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

        Action spawn = () => _spawner.Spawn(scene, _world);

        spawn.Should().Throw<InvalidDataException>().WithMessage("*'Lamp'*'trasform'*");
        _world.Size.Should().Be(0);
    }

    [Fact]
    public void InvalidComponentDataNamesTheEntityAndComponent()
    {
        SceneData scene = Parse("""{ "entities": [ { "name": "Crate", "components": { "transform": { "position": [1, 2] } } } ] }""");

        Action spawn = () => _spawner.Spawn(scene, _world);

        spawn.Should().Throw<InvalidDataException>().WithMessage("*'Crate'*'transform'*");
        _world.Size.Should().Be(0);
    }

    [Fact]
    public void RegisteringANameTwiceThrows()
    {
        Action registerAgain = () => _spawner.Register("transform", RuntimeJsonContext.Default.Transform);

        registerAgain.Should().Throw<ArgumentException>();
    }

    private static SceneData Parse(string json)
    {
        SceneData? scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData);
        scene.Should().NotBeNull();
        return scene;
    }
}
