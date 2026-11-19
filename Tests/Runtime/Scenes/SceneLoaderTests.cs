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

    [Fact]
    public void SavesTheWorldIntoTheSceneFile()
    {
        SceneLoader loader = _builder.WithScene("Room.scene.json", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.scene.json");
        Vector3 raised = new(0, 3, 0);
        _builder.World.Get<Transform>(entities[0]).Position = raised;

        loader.Save("Room.scene.json");

        Transform saved = _builder.LoadInNewWorld("Room.scene.json");
        saved.Position.Should().Be(raised);
    }

    [Fact]
    public void SavesComponentsTheWorldDidNotChangeAsTheFileWroteThem()
    {
        SceneLoader loader = _builder.WithScene("Room.scene.json", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } }, { "name": "Box", "components": { "transform": { "position": [1, 0, 0] } } } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.scene.json");
        _builder.World.Get<Transform>(entities[0]).Position = Vector3.UnitY;

        loader.Save("Room.scene.json");

        string box = _builder.ReadComponent("Room.scene.json", entity: 1, "transform");
        box.Should().Be("""{"position":[1,0,0]}""");
    }

    [Fact]
    public void KeepsTheEntitiesWhenItSaves()
    {
        SceneLoader loader = _builder.WithScene("Room.scene.json", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.scene.json");
        _builder.World.Get<Transform>(entities[0]).Position = Vector3.UnitY;

        loader.Save("Room.scene.json");

        _builder.World.IsAlive(entities[0]).Should().BeTrue();
        _builder.World.Get<Transform>(entities[0]).Position.Should().Be(Vector3.UnitY);
        _builder.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void SavingASceneThatIsNotLoadedThrows()
    {
        SceneLoader loader = _builder.Build();

        Action save = () => loader.Save("Room.scene.json");

        save.Should().Throw<InvalidOperationException>();
    }
}
