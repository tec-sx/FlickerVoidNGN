using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Runtime.Components;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class EditorSceneTests : IDisposable
{
    private readonly EditorSceneTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SavesTheEditedWorldIntoTheOpenSceneFile()
    {
        Entity lamp = _builder.WithScene("Scenes/Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""").Open("Scenes/Room.fvscene");
        Vector3 moved = new(2, 1, 0);
        _builder.Scene.Game.World.Get<Transform>(lamp).Position = moved;

        bool saved = _builder.Scene.Save();

        saved.Should().BeTrue();
        _builder.ReadScene("Scenes/Room.fvscene").Should().Contain("\"position\":[2,1,0]");
        _builder.Scene.Game.World.Get<Transform>(lamp).Position.Should().Be(moved);
    }

    [Fact]
    public void CannotSaveAWorldWithoutASceneFile()
    {
        bool saved = _builder.Scene.Save();

        saved.Should().BeFalse();
    }

    [Fact]
    public void OpeningAnotherSceneReplacesTheWorldAndClearsTheSelection()
    {
        Entity lamp = _builder
            .WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""")
            .WithScene("Hall.fvscene", """{ "entities": [ { "name": "Door", "components": { "transform": {} } } ] }""")
            .Open("Room.fvscene");
        _builder.Scene.Selection.Select(lamp);

        _builder.Scene.Open("Hall.fvscene");

        World world = _builder.Scene.Game.World;
        world.IsAlive(lamp).Should().BeFalse();
        world.CountEntities(new QueryDescription()).Should().Be(1);
        _builder.Scene.Selection.Selected.Should().BeNull();
        _builder.Scene.ScenePath.Should().Be("Hall.fvscene");
    }

    [Fact]
    public void AFailedOpenKeepsTheOpenScene()
    {
        Entity lamp = _builder
            .WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""")
            .WithScene("Broken.fvscene", """{ "entities": [ { "name": "Door", "components": { "unknown": {} } } ] }""")
            .Open("Room.fvscene");

        Action open = () => _builder.Scene.Open("Broken.fvscene");

        open.Should().Throw<InvalidDataException>();
        _builder.Scene.Game.World.IsAlive(lamp).Should().BeTrue();
        _builder.Scene.ScenePath.Should().Be("Room.fvscene");
    }

    [Fact]
    public void SavesAnAddedPresetWithAUniqueName()
    {
        _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Point Light", "components": { "transform": {} } } ] }""").Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        EntityPreset light = EntityPreset.All.Single(preset => preset.Name == "Point Light");

        Entity added = scene.AddEntity(light.Create(scene.Game.World, scene.Entities, new Vector3(1, 2, 3)));
        scene.Save();

        scene.Entities.Should().HaveCount(2).And.Contain(added);
        string saved = _builder.ReadScene("Room.fvscene");
        saved.Should().Contain("Point Light 2").And.Contain("pointLight");
    }

    [Fact]
    public void AddsEveryPreset()
    {
        _builder.WithScene("Room.fvscene", """{ "entities": [] }""").Open("Room.fvscene");
        EditorScene scene = _builder.Scene;

        foreach (EntityPreset preset in EntityPreset.All)
        {
            EntityData data = preset.Create(scene.Game.World, scene.Entities, Vector3.Zero);
            scene.AddEntity(data);
        }

        scene.Entities.Should().HaveCount(EntityPreset.All.Count);
    }

    [Fact]
    public void CannotAddAnEntityWithoutAScene()
    {
        Action add = () => _builder.Scene.AddEntity(new FlickerVoid.Engine.Runtime.Scenes.EntityData { Name = "Box" });

        add.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DestroyedEntitiesAreLeftOutOfTheSave()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""").Open("Room.fvscene");

        _builder.Scene.DestroyEntity(lamp);
        _builder.Scene.Save();

        _builder.Scene.Entities.Should().BeEmpty();
        _builder.ReadScene("Room.fvscene").Should().NotContain("Lamp");
    }
}
