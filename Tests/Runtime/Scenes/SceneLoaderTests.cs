using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Scenes;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

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
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp" } ] }""").Build();

        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        EntityName name = _builder.World.Get<EntityName>(entities[0]);
        name.Value.Should().Be("Lamp");
    }

    [Fact]
    public void PatchesTheWorldWhenTheFileChanges()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        _builder.ChangeScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": { "position": [0, 3, 0] } } } ] }""");

        Transform lamp = _builder.World.Get<Transform>(entities[0]);
        Vector3 raised = new(0, 3, 0);
        lamp.Position.Should().Be(raised);
    }

    [Fact]
    public void KeepsTheWorldAndLogsWhenTheChangedFileIsInvalid()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp" } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        _builder.ChangeScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "unknown": {} } } ] }""");
        _builder.ChangeScene("Room.fvscene", """{ "entities": [ { "name": "Lamp" """);

        _builder.World.IsAlive(entities[0]).Should().BeTrue();
        _builder.Warnings.Should().HaveCount(2);
    }

    [Fact]
    public void IgnoresChangesToOtherFiles()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp" } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        _builder.ChangeScene("Other.fvscene", """{ "entities": [] }""");

        _builder.World.IsAlive(entities[0]).Should().BeTrue();
        _builder.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void SavesAnAddedEntityWithTheScene()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""").Build();
        IReadOnlyList<Entity> loaded = loader.Load("Room.fvscene");
        EntityData data = new() { Name = "Box" };
        data.Components.Add("transform", System.Text.Json.JsonDocument.Parse("""{ "position": [2, 0, 0] }""").RootElement);

        Entity added = loader.Add("Room.fvscene", data);
        loader.Save("Room.fvscene");

        loader.GetEntities("Room.fvscene").Should().Equal(loaded[0], added);
        _builder.ReadComponent("Room.fvscene", 1, "transform").Should().Contain("[2,0,0]");
    }

    [Fact]
    public void SavesTheWorldIntoTheSceneFile()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");
        Vector3 raised = new(0, 3, 0);
        _builder.World.Get<Transform>(entities[0]).Position = raised;

        loader.Save("Room.fvscene");

        Transform saved = _builder.LoadInNewWorld("Room.fvscene");
        saved.Position.Should().Be(raised);
    }

    [Fact]
    public void SavesComponentsTheWorldDidNotChangeAsTheFileWroteThem()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } }, { "name": "Box", "components": { "transform": { "position": [1, 0, 0] } } } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");
        _builder.World.Get<Transform>(entities[0]).Position = Vector3.UnitY;

        loader.Save("Room.fvscene");

        string box = _builder.ReadComponent("Room.fvscene", entity: 1, "transform");
        box.Should().Be("""{"position":[1,0,0]}""");
    }

    [Fact]
    public void KeepsTheEntitiesWhenItSaves()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");
        _builder.World.Get<Transform>(entities[0]).Position = Vector3.UnitY;

        loader.Save("Room.fvscene");

        _builder.World.IsAlive(entities[0]).Should().BeTrue();
        _builder.World.Get<Transform>(entities[0]).Position.Should().Be(Vector3.UnitY);
        _builder.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void SavingASceneThatIsNotLoadedThrows()
    {
        SceneLoader loader = _builder.Build();

        Action save = () => loader.Save("Room.fvscene");

        save.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UnloadingDestroysTheScenesEntitiesAndStopsFollowingTheFile()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp" } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        loader.Unload("Room.fvscene");
        _builder.ChangeScene("Room.fvscene", """{ "entities": [ { "name": "Lamp" }, { "name": "Box" } ] }""");

        _builder.World.IsAlive(entities[0]).Should().BeFalse();
        _builder.World.CountEntities(new QueryDescription()).Should().Be(0);
        IReadOnlyList<Entity> reloaded = loader.Load("Room.fvscene");
        reloaded.Should().HaveCount(2);
    }

    [Fact]
    public void KeepsTheWorldAndLogsWhenTheFileIsDeleted()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp" } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        _builder.DeleteScene("Room.fvscene");

        _builder.World.IsAlive(entities[0]).Should().BeTrue();
        _builder.Warnings.Should().ContainSingle();
    }
}
