using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

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

    [Fact]
    public void PatchSetsOnlyTheComponentsAFileChanged()
    {
        SceneData before = Parse("""
            { "entities": [
                { "name": "Lamp", "components": { "transform": { "position": [1, 2, 3] } } },
                { "name": "Fox", "components": { "transform": { "position": [0, 0, 0] } } } ] }
            """);
        SceneData after = Parse("""
            { "entities": [
                { "name": "Lamp", "components": { "transform": { "position": [4, 5, 6] } } },
                { "name": "Fox", "components": { "transform": { "position": [0, 0, 0] } } } ] }
            """);
        IReadOnlyList<Entity> entities = _spawner.Spawn(before, _world);
        Vector3 walked = new(7, 0, 7);
        _world.Set(entities[1], new Transform { Position = walked });

        IReadOnlyList<Entity> patched = _spawner.Patch(before, after, entities, _world);

        patched.Should().Equal(entities);
        Transform lamp = _world.Get<Transform>(patched[0]);
        Transform fox = _world.Get<Transform>(patched[1]);
        Vector3 moved = new(4, 5, 6);
        lamp.Position.Should().Be(moved);
        fox.Position.Should().Be(walked, "the file left the fox's transform as it was, so the game's changes stay");
    }

    [Fact]
    public void DescribesEveryRegisteredComponentInTheSceneSchema()
    {
        JsonObject schema = _spawner.GetSchema();

        JsonNode components = schema["properties"]!["entities"]!["items"]!["properties"]!["components"]!;
        JsonNode position = components["properties"]!["transform"]!["properties"]!["position"]!;
        position["type"]!.GetValue<string>().Should().Be("array");
        position["minItems"]!.GetValue<int>().Should().Be(3);
        components["additionalProperties"]!.GetValue<bool>().Should().BeFalse("a misspelt component should be flagged");
    }

    [Fact]
    public void ListsTheRegisteredComponentsWithTheirMetadata()
    {
        IReadOnlyList<SceneComponent> components = _spawner.Components;

        components.Should().ContainSingle();
        components[0].Name.Should().Be("transform");
        components[0].TypeInfo.Type.Should().Be<Transform>();
    }

    [Fact]
    public void GetsAndSetsAComponentOfAnEntityAsABoxedValue()
    {
        SceneData scene = Parse("""{ "entities": [ { "components": { "transform": { "position": [1, 2, 3] } } }, {} ] }""");
        IReadOnlyList<Entity> entities = _spawner.Spawn(scene, _world);
        SceneComponent transform = _spawner.Components[0];
        Vector3 moved = new(4, 5, 6);

        JsonPropertyInfo position = transform.TypeInfo.Properties.Single(property => property.Name == "position");

        object component = transform.Get(_world, entities[0]);
        position.Set!(component, moved);
        transform.Set(_world, entities[0], component);

        Transform stored = _world.Get<Transform>(entities[0]);
        stored.Position.Should().Be(moved);
        transform.Has(_world, entities[0]).Should().BeTrue();
        transform.Has(_world, entities[1]).Should().BeFalse();
    }

    [Fact]
    public void ReadsASceneThatNamesItsSchema()
    {
        SceneData scene = Parse("""{ "$schema": "../.schemas/scene.schema.json", "entities": [ { "name": "Lamp" } ] }""");

        scene.Entities.Should().ContainSingle();
    }

    [Fact]
    public void PatchSpawnsAddedEntitiesAndDestroysRemovedOnes()
    {
        SceneData before = Parse("""{ "entities": [ { "name": "Lamp" }, { "name": "Crate" } ] }""");
        SceneData after = Parse("""{ "entities": [ { "name": "Crate" }, { "name": "Table" } ] }""");
        IReadOnlyList<Entity> entities = _spawner.Spawn(before, _world);

        IReadOnlyList<Entity> patched = _spawner.Patch(before, after, entities, _world);

        patched.Should().HaveCount(2);
        patched[0].Should().Be(entities[1], "the crate is kept");
        _world.IsAlive(entities[0]).Should().BeFalse("the lamp is gone from the file");
        EntityName table = _world.Get<EntityName>(patched[1]);
        table.Value.Should().Be("Table");
    }

    [Fact]
    public void PatchAddsAndRemovesComponents()
    {
        SceneData before = Parse("""{ "entities": [ { "name": "Lamp", "components": { "transform": {} } }, { "name": "Crate" } ] }""");
        SceneData after = Parse("""{ "entities": [ { "name": "Lamp" }, { "name": "Crate", "components": { "transform": {} } } ] }""");
        IReadOnlyList<Entity> entities = _spawner.Spawn(before, _world);

        IReadOnlyList<Entity> patched = _spawner.Patch(before, after, entities, _world);

        _world.Has<Transform>(patched[0]).Should().BeFalse();
        _world.Has<Transform>(patched[1]).Should().BeTrue();
    }

    [Fact]
    public void PatchKeepsUnnamedEntitiesWhoseDataIsUnchanged()
    {
        SceneData before = Parse("""{ "entities": [ { "components": { "transform": { "position": [1, 0, 0] } } } ] }""");
        SceneData after = Parse("""{ "entities": [ { "components": { "transform": { "position": [1, 0, 0] } } }, { "name": "Lamp" } ] }""");
        IReadOnlyList<Entity> entities = _spawner.Spawn(before, _world);

        IReadOnlyList<Entity> patched = _spawner.Patch(before, after, entities, _world);

        patched[0].Should().Be(entities[0]);
    }

    private static SceneData Parse(string json)
    {
        SceneData? scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData);
        scene.Should().NotBeNull();
        return scene;
    }
}
