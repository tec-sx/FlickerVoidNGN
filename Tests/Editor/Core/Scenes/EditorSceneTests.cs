using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Scenes;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class EditorSceneTests : IDisposable
{
    private const string Room = """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""";

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
    public void AttachesButNeverActivatesEntitiesAndRestartsComponentsItChanges()
    {
        List<string> calls = [];
        EntityLifecycle lifecycle = _builder.Scene.Game.Lifecycle;
        lifecycle.OnAttach((Entity _, ref Transform transform) => calls.Add($"attach {transform.Position.X}"));
        lifecycle.OnActivate((Entity _, ref Transform _) => calls.Add("activate"));
        lifecycle.OnDetach((Entity _, ref Transform transform) => calls.Add($"detach {transform.Position.X}"));
        Entity lamp = _builder.WithScene("Scenes/Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""").Open("Scenes/Room.fvscene");
        Transform moved = new() { Position = new Vector3(2, 0, 0) };

        _builder.Scene.SetComponent(lamp, moved);
        _builder.Scene.DestroyEntity(lamp);

        calls.Should().Equal("attach 0", "detach 0", "attach 2", "detach 2");
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
    public void DuplicatesAnEntityWithItsComponentsUnderTheNextFreeName()
    {
        _builder
            .WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": { "position": [1, 2, 3] }, "pointLight": { "range": 7 } } }, { "name": "Lamp 2", "components": { "transform": {} } } ] }""")
            .Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        Entity lamp = scene.Entities[0];

        Entity copy = scene.DuplicateEntity(lamp);
        scene.Save();

        World world = scene.Game.World;
        world.Get<EntityName>(copy).Value.Should().Be("Lamp 3");
        world.Get<Transform>(copy).Position.Should().Be(new Vector3(1, 2, 3));
        world.Get<PointLight>(copy).Range.Should().Be(7);
        scene.Entities.Should().HaveCount(3);
        _builder.ReadScene("Room.fvscene").Should().Contain("Lamp 3");
    }

    [Fact]
    public void NumbersTheCopyOfANumberedEntityFromItsBaseName()
    {
        _builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp 2", "components": { "transform": {} } } ] }""");
        Entity lamp = _builder.Open("Room.fvscene");

        Entity copy = _builder.Scene.DuplicateEntity(lamp);

        _builder.Scene.Game.World.Get<EntityName>(copy).Value.Should().Be("Lamp 3");
        _builder.Scene.IsUnsaved.Should().BeTrue();
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

    [Fact]
    public void AnOpenedSceneHasNoUnsavedEdits()
    {
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        _builder.Scene.IsUnsaved.Should().BeFalse();
    }

    [Fact]
    public void SettingAComponentChangesTheEntityAndLeavesTheSceneUnsaved()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        int edits = 0;
        scene.Edited += () => edits++;
        Transform moved = new() { Position = new Vector3(2, 1, 0) };

        scene.SetComponent(lamp, moved);

        scene.Game.World.Get<Transform>(lamp).Position.Should().Be(moved.Position);
        scene.IsUnsaved.Should().BeTrue();
        edits.Should().Be(1);
    }

    [Fact]
    public void SettingAComponentByItsSceneNameChangesTheEntity()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        SceneComponent transform = _builder.FindComponent("transform");
        object moved = new Transform { Position = new Vector3(0, 3, 0) };

        scene.SetComponent(lamp, transform, moved);

        scene.Game.World.Get<Transform>(lamp).Position.Should().Be(new Vector3(0, 3, 0));
        scene.IsUnsaved.Should().BeTrue();
    }

    [Fact]
    public void AddingAComponentGivesItItsDefaultsAndRemovingItTakesItAway()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        SceneComponent light = _builder.FindComponent("pointLight");
        int edits = 0;
        scene.Edited += () => edits++;

        scene.AddComponent(lamp, light);
        bool added = scene.Game.World.Has<PointLight>(lamp);
        float range = scene.Game.World.Get<PointLight>(lamp).Range;
        scene.RemoveComponent(lamp, light);

        added.Should().BeTrue();
        range.Should().Be(new PointLight().Range);
        scene.Game.World.Has<PointLight>(lamp).Should().BeFalse();
        scene.IsUnsaved.Should().BeTrue();
        edits.Should().Be(2);
    }

    [Fact]
    public void AddingAComponentTheEntityHasChangesNothing()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        SceneComponent transform = _builder.FindComponent("transform");
        Transform moved = new() { Position = new Vector3(5, 0, 0) };
        scene.Game.World.Set(lamp, moved);

        scene.AddComponent(lamp, transform);

        scene.Game.World.Get<Transform>(lamp).Position.Should().Be(moved.Position);
        scene.IsUnsaved.Should().BeFalse();
    }

    [Fact]
    public void AddingAndDestroyingEntitiesLeaveTheSceneUnsaved()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        int edits = 0;
        scene.Edited += () => edits++;

        scene.AddEntity(new EntityData { Name = "Box" });
        scene.DestroyEntity(lamp);

        scene.IsUnsaved.Should().BeTrue();
        edits.Should().Be(2);
    }

    [Fact]
    public void SavingAndOpeningClearTheUnsavedEdits()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).WithScene("Hall.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        Transform moved = new() { Position = Vector3.One };
        scene.SetComponent(lamp, moved);
        scene.Save();
        bool afterSave = scene.IsUnsaved;
        Entity first = scene.Entities[0];
        scene.SetComponent(first, moved);

        scene.Open("Hall.fvscene");

        afterSave.Should().BeFalse();
        scene.IsUnsaved.Should().BeFalse();
    }

    [Fact]
    public void ReportsOnlyWhenTheSceneBecomesUnsavedOrSaved()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        List<bool> reported = [];
        scene.UnsavedChanged += () => reported.Add(scene.IsUnsaved);
        Transform moved = new() { Position = Vector3.One };

        scene.SetComponent(lamp, moved);
        scene.SetComponent(lamp, moved);
        scene.Save();

        reported.Should().Equal(true, false);
    }

    [Fact]
    public void EditingADestroyedEntityChangesNothing()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        scene.Game.World.Destroy(lamp);
        SceneComponent light = _builder.FindComponent("pointLight");

        scene.SetComponent(lamp, new Transform());
        scene.AddComponent(lamp, light);
        scene.RemoveComponent(lamp, light);

        scene.IsUnsaved.Should().BeFalse();
    }
}
