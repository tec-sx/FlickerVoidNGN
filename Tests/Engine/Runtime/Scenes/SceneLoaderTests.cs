using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Tests.Entities;

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
    public void SpawnsTheSceneFromContentsGivenInPlaceOfTheFileAndSavesToTheFile()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp" } ] }""").Build();
        byte[] edited = System.Text.Encoding.UTF8.GetBytes("""{ "entities": [ { "name": "Lamp", "folder": "Lights" }, { "name": "Box" } ] }""");

        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene", edited);

        entities.Should().HaveCount(2);
        _builder.World.Get<EntityFolder>(entities[0]).Path.Should().Be("Lights");
        _builder.ReadFile("Room.fvscene").Should().NotContain("Box", "the contents are not saved until the scene is");
        loader.Save("Room.fvscene");
        _builder.ReadFile("Room.fvscene").Should().Contain("Box");
    }

    [Fact]
    public void PutsEntitiesInTheFoldersTheSceneNames()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "folder": "Lights/Hall" }, { "name": "Box" } ] }""").Build();

        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        _builder.World.Get<EntityFolder>(entities[0]).Path.Should().Be("Lights/Hall");
        _builder.World.Has<EntityFolder>(entities[1]).Should().BeFalse();
    }

    [Fact]
    public void SavesTheFoldersOfEntities()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "folder": "Lights" }, { "name": "Box" } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");
        _builder.World.Remove<EntityFolder>(entities[0]);
        _builder.World.Add(entities[1], new EntityFolder("Props"));

        loader.Save("Room.fvscene");

        string text = _builder.ReadFile("Room.fvscene");
        text.Should().Contain("\"folder\":\"Props\"").And.NotContain("Lights");
    }

    [Fact]
    public void MovesEntitiesBetweenFoldersWhenTheFileChanges()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "folder": "Lights" }, { "name": "Box" } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        _builder.ChangeScene("Room.fvscene", """{ "entities": [ { "name": "Lamp" }, { "name": "Box", "folder": "Props" } ] }""");

        _builder.World.Has<EntityFolder>(entities[0]).Should().BeFalse();
        _builder.World.Get<EntityFolder>(entities[1]).Path.Should().Be("Props");
    }

    [Fact]
    public void NestsEntitiesUnderTheParentsTheSceneNames()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Trigger" }, { "name": "Enter", "parent": "Trigger" } ] }""").Build();

        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        _builder.World.Has<EntityParent>(entities[0]).Should().BeFalse();
        _builder.World.Get<EntityParent>(entities[1]).Name.Should().Be("Trigger");
    }

    [Fact]
    public void SavesTheParentsOfEntities()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Trigger" }, { "name": "Enter", "parent": "Trigger" }, { "name": "Exit" } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");
        _builder.World.Remove<EntityParent>(entities[1]);
        _builder.World.Add(entities[2], new EntityParent("Trigger"));

        loader.Save("Room.fvscene");

        string text = _builder.ReadFile("Room.fvscene");
        text.Should().Contain("""{"name":"Exit","parent":"Trigger",""").And.Contain(""""{"name":"Enter","components"""");
    }

    [Fact]
    public void MovesEntitiesBetweenParentsWhenTheFileChanges()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Trigger" }, { "name": "Enter", "parent": "Trigger" }, { "name": "Exit" } ] }""").Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        _builder.ChangeScene("Room.fvscene", """{ "entities": [ { "name": "Trigger" }, { "name": "Enter" }, { "name": "Exit", "parent": "Trigger" } ] }""");

        _builder.World.Has<EntityParent>(entities[1]).Should().BeFalse();
        _builder.World.Get<EntityParent>(entities[2]).Name.Should().Be("Trigger");
    }

    [Fact]
    public void SpawnsEntitiesFromTheirTemplatesUnderTheirOwnValues()
    {
        SceneLoader loader = _builder
            .WithScene("Props/Lamp.fventity", """{ "components": { "transform": { "position": [0, 2, 0], "scale": [2, 2, 2] } } }""")
            .WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "template": "Props/Lamp.fventity", "components": { "transform": { "position": [1, 0, 0] } } } ] }""")
            .Build();

        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        Transform lamp = _builder.World.Get<Transform>(entities[0]);
        lamp.Position.Should().Be(new Vector3(1, 0, 0));
        lamp.Scale.Should().Be(new Vector3(2, 2, 2));
    }

    [Fact]
    public void FollowsATemplatesBase()
    {
        SceneLoader loader = _builder
            .WithScene("Props/Lamp.fventity", """{ "components": { "transform": { "scale": [2, 2, 2] } } }""")
            .WithScene("Props/TallLamp.fventity", """{ "base": "Props/Lamp.fventity", "components": { "transform": { "position": [0, 3, 0] } } }""")
            .WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "template": "Props/TallLamp.fventity" } ] }""")
            .Build();

        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        Transform lamp = _builder.World.Get<Transform>(entities[0]);
        lamp.Position.Should().Be(new Vector3(0, 3, 0));
        lamp.Scale.Should().Be(new Vector3(2, 2, 2));
    }

    [Fact]
    public void RefusesATemplateThatIsItsOwnBase()
    {
        SceneLoader loader = _builder
            .WithScene("Props/Lamp.fventity", """{ "base": "Props/Lamp.fventity" }""")
            .WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "template": "Props/Lamp.fventity" } ] }""")
            .Build();

        Action load = () => loader.Load("Room.fvscene");

        load.Should().Throw<InvalidDataException>().WithMessage("*Props/Lamp.fventity*");
    }

    [Fact]
    public void SavesOnlyWhatDiffersFromTheTemplate()
    {
        SceneLoader loader = _builder
            .WithScene("Props/Lamp.fventity", """{ "components": { "transform": { "position": [0, 2, 0], "scale": [2, 2, 2] } } }""")
            .WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "template": "Props/Lamp.fventity" }, { "name": "Moved", "template": "Props/Lamp.fventity" } ] }""")
            .Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");
        _builder.World.Get<Transform>(entities[1]).Position = new Vector3(5, 0, 0);

        loader.Save("Room.fvscene");

        string saved = _builder.ReadFile("Room.fvscene");
        saved.Should().Contain("""{"name":"Lamp","template":"Props/Lamp.fventity","components":{}}""");
        saved.Should().Contain("""{"name":"Moved","template":"Props/Lamp.fventity","components":{"transform":{"position":[5,0,0]}}}""");
    }

    [Fact]
    public void PatchesTheEntitiesOfATemplateThatChanges()
    {
        SceneLoader loader = _builder
            .WithScene("Props/Lamp.fventity", """{ "components": { "transform": { "scale": [2, 2, 2] } } }""")
            .WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "template": "Props/Lamp.fventity" } ] }""")
            .Build();
        IReadOnlyList<Entity> entities = loader.Load("Room.fvscene");

        _builder.ChangeScene("Props/Lamp.fventity", """{ "components": { "transform": { "scale": [3, 3, 3] } } }""");

        Transform lamp = _builder.World.Get<Transform>(entities[0]);
        lamp.Scale.Should().Be(new Vector3(3, 3, 3));
    }

    [Fact]
    public void SpawnsATemplateFromCodeAndStartsIt()
    {
        SceneLoader loader = _builder
            .WithScene("Props/Lamp.fventity", """{ "components": { "transform": { "position": [0, 2, 0], "scale": [2, 2, 2] } } }""")
            .Build();
        Transform placed = new() { Position = new Vector3(4, 0, 0) };

        Entity lamp = loader.Templates.Spawn("Props/Lamp.fventity", _builder.World, placed);

        Transform transform = _builder.World.Get<Transform>(lamp);
        transform.Position.Should().Be(new Vector3(4, 0, 0), "the placed transform replaces the template's");
        _builder.World.Has<Attached>(lamp).Should().BeTrue();
    }

    [Fact]
    public void SpawnsAnEntityClassWithItsComponentsAtTheirDefaults()
    {
        SceneLoader loader = _builder.Build();
        Transform placed = new() { Position = new Vector3(4, 0, 0) };

        Lamp lamp = loader.Templates.Spawn<Lamp>(_builder.World, placed);

        lamp.Glow.Intensity.Should().Be(1);
        lamp.Transform.Position.Should().Be(new Vector3(4, 0, 0));
        _builder.World.Has<Attached>(lamp.Entity).Should().BeTrue();
    }

    [Fact]
    public void SpawnsAnEntityClassFromItsTemplate()
    {
        SceneLoader loader = _builder
            .WithScene("Props/Lantern.fventity", """{ "components": { "glow": { "intensity": 4 } } }""")
            .Build();
        Transform placed = new() { Position = new Vector3(0, 1, 0) };

        Lantern lantern = loader.Templates.Spawn<Lantern>(_builder.World, placed);

        lantern.Glow.Intensity.Should().Be(4);
        _builder.World.Get<Transform>(lantern.Entity).Position.Should().Be(new Vector3(0, 1, 0));
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
    public void WarnsOfEntitiesThatShareANameOrASaveId()
    {
        SceneLoader loader = _builder.WithScene("Room.fvscene", """
            { "entities": [
              { "name": "Lamp", "components": { "saveable": { "id": "Crate" } } },
              { "name": "Lamp", "components": { "saveable": { "id": "Crate" } } },
              { "name": "Box", "components": { "saveable": { "id": "Box" } } }
            ] }
            """).Build();

        loader.Load("Room.fvscene");

        string[] warnings = [.. _builder.Warnings.Select(static arguments => arguments[2]?.ToString() ?? "")];
        warnings.Should().HaveCount(2);
        warnings.Should().Contain(warning => warning.Contains("Lamp")).And.Contain(warning => warning.Contains("Crate"));
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
