using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Engine.Runtime.Tests.Scenes;

public sealed class SceneLoaderTests : IDisposable
{
    private readonly SceneLoaderTestsBuilder _builder = new();

    public void Dispose()
    {
        _builder.Dispose();
    }

    [Fact]
    public void SpawnsTheSceneFile()
    {
        SceneLoader loader = _builder.WithScene("Room.scene.json", """{ "entities": [ { "name": "Lamp" } ] }""").Build();

        IReadOnlyList<Entity> entities = loader.Load("Room.scene.json");

        EntityName name = _builder.World.Get<EntityName>(entities[0]);
        name.Value.Should().Be("Lamp");
    }

    [Fact]
    public void PatchesTheWorldWhenTheFileChanges()
    {
        SceneLoader loader = _builder.WithScene("Room.scene.json", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.scene.json");

        _builder.ChangeScene("Room.scene.json", """{ "entities": [ { "name": "Lamp", "components": { "transform": { "position": [0, 3, 0] } } } ] }""");

        Transform lamp = _builder.World.Get<Transform>(entities[0]);
        Vector3 raised = new(0, 3, 0);
        lamp.Position.Should().Be(raised);
    }

    [Fact]
    public void KeepsTheWorldAndLogsWhenTheChangedFileIsInvalid()
    {
        SceneLoader loader = _builder.WithScene("Room.scene.json", """{ "entities": [ { "name": "Lamp" } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.scene.json");

        _builder.ChangeScene("Room.scene.json", """{ "entities": [ { "name": "Lamp", "components": { "unknown": {} } } ] }""");
        _builder.ChangeScene("Room.scene.json", """{ "entities": [ { "name": "Lamp" """);

        _builder.World.IsAlive(entities[0]).Should().BeTrue();
        _builder.Warnings.Should().HaveCount(2);
    }

    [Fact]
    public void IgnoresChangesToOtherFiles()
    {
        SceneLoader loader = _builder.WithScene("Room.scene.json", """{ "entities": [ { "name": "Lamp" } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.scene.json");

        _builder.ChangeScene("Other.scene.json", """{ "entities": [] }""");

        _builder.World.IsAlive(entities[0]).Should().BeTrue();
        _builder.Warnings.Should().BeEmpty();
    }
}
