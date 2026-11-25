using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Tests.Projects;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class EditorSceneReloadTests : IDisposable
{
    private const string Room = """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } }, { "name": "Box", "components": { "transform": {} } } ] }""";

    private readonly EditorSceneTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void KeepsTheUnsavedEditsOfTheOpenSceneInANewGame()
    {
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        Entity lamp = scene.Find("Lamp")!.Value;
        Game before = scene.Game;
        Transform moved = new() { Position = new Vector3(2, 1, 0) };
        scene.SetComponent(lamp, moved);

        scene.Reload([]);

        scene.Game.Should().NotBeSameAs(before);
        Entity reloaded = scene.Find("Lamp")!.Value;
        scene.Game.World.Get<Transform>(reloaded).Position.Should().Be(moved.Position);
        scene.Entities.Should().HaveCount(2);
        scene.ScenePath.Should().Be("Room.fvscene");
        scene.IsUnsaved.Should().BeTrue();
        _builder.ReadScene("Room.fvscene").Should().NotContain("position", "reloading code saves nothing");
    }

    [Fact]
    public void KnowsTheComponentsOfTheReloadedModules()
    {
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scene;

        scene.Reload([new SpinModule()]);

        SceneComponent spin = _builder.FindComponent("spin");
        Entity reloaded = scene.Find("Lamp")!.Value;
        scene.AddComponent(reloaded, spin);
        scene.Game.World.Has<Spin>(reloaded).Should().BeTrue();
    }

    [Fact]
    public void PicksTheMeshesTheReloadedModulesMake()
    {
        _builder.WithFile("Van.fvtest", "2");
        EditorScene scene = _builder.Scene;

        scene.Reload([new TestMeshSourceModule()]);

        Transform place = new() { Position = new Vector3(0, -1, 0) };
        MeshRenderer renderer = new() { Mesh = "Van.fvtest#body" };
        Entity van = scene.Game.World.Create(place, renderer);
        Ray downTheZAxis = new(new Vector3(0, 0, 10), -Vector3.UnitZ);
        Entity? picked = scene.Picker.Pick(scene.Game.World, downTheZAxis);
        picked.Should().Be(van);
    }

    [Fact]
    public void SelectsTheSameEntitiesByName()
    {
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        Entity lamp = scene.Find("Lamp")!.Value;
        Entity box = scene.Find("Box")!.Value;
        scene.Selection.SelectAll([lamp, box], box);

        scene.Reload([]);

        Entity reloadedBox = scene.Find("Box")!.Value;
        scene.Selection.Selected.Should().Be(reloadedBox);
        scene.Selection.Entities.Should().HaveCount(2);
    }

    [Fact]
    public void ForgetsTheUndoStepsOfTheOldCodeAndReportsTheReload()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scene;
        scene.SetComponent(lamp, new Transform { Position = Vector3.One });
        List<string> raised = [];
        scene.Opened += () => raised.Add("opened");
        scene.EntitiesChanged += () => raised.Add("entities");

        scene.Reload([]);

        scene.History.CanUndo.Should().BeFalse("undo steps hold the old code's components");
        raised.Should().Equal("opened", "entities");
    }

    [Fact]
    public void ReloadsAWorldWithoutASceneFile()
    {
        EditorScene scene = _builder.Scene;

        scene.Reload([new SpinModule()]);

        scene.ScenePath.Should().BeNull();
        scene.IsUnsaved.Should().BeFalse();
        _builder.FindComponent("spin").Should().NotBeNull();
    }

    [Fact]
    public void KeepsTheOldGameWhenTheNewCodeLacksAComponentTheSceneUses()
    {
        using EditorSceneTestsBuilder builder = new([new SpinModule()]);
        builder.WithScene("Room.fvscene", """{ "entities": [ { "name": "Wheel", "components": { "spin": { "speed": 2 } } } ] }""").Open("Room.fvscene");
        EditorScene scene = builder.Scene;
        Game before = scene.Game;

        Action reload = () => scene.Reload([]);

        reload.Should().Throw<InvalidDataException>().WithMessage("*spin*");
        scene.Game.Should().BeSameAs(before);
        Entity wheel = scene.Find("Wheel")!.Value;
        scene.Game.World.Get<Spin>(wheel).Speed.Should().Be(2);
    }
}
