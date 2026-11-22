using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class EditorSceneUndoTests : IDisposable
{
    private const string Room = """{ "entities": [ { "name": "Lamp", "folder": "Lights", "components": { "transform": { "position": [1, 0, 0] }, "pointLight": { "range": 7 } } } ] }""";

    private readonly EditorSceneTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    private World World => _builder.Scene.Game.World;

    [Fact]
    public void UndoesAndRedoesAComponentSet()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        Transform moved = new() { Position = new Vector3(5, 0, 0) };

        _builder.Scene.SetComponent(lamp, moved);
        _builder.Scene.History.Undo();

        World.Get<Transform>(lamp).Position.Should().Be(new Vector3(1, 0, 0));
        _builder.Scene.History.Redo();
        World.Get<Transform>(lamp).Position.Should().Be(new Vector3(5, 0, 0));
    }

    [Fact]
    public void RecordsADragAsOneStep()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        _builder.Scene.History.BeginGroup("Move Lamp");
        for (int x = 2; x <= 4; x++)
        {
            Transform moved = new() { Position = new Vector3(x, 0, 0) };
            _builder.Scene.SetComponent(lamp, moved);
        }

        _builder.Scene.History.EndGroup();
        _builder.Scene.History.UndoName.Should().Be("Move Lamp");
        _builder.Scene.History.Undo();

        World.Get<Transform>(lamp).Position.Should().Be(new Vector3(1, 0, 0));
        _builder.Scene.History.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void UndoesAddingAndRemovingComponents()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        SceneComponent light = _builder.FindComponent("pointLight");

        _builder.Scene.RemoveComponent(lamp, light);
        _builder.Scene.History.Undo();

        World.Get<PointLight>(lamp).Range.Should().Be(7);
        _builder.Scene.History.Redo();
        World.Has<PointLight>(lamp).Should().BeFalse();
        _builder.Scene.History.Undo();
        _builder.Scene.History.Undo();
        _builder.Scene.History.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void BringsADeletedEntityBackWithItsComponentsAndFolder()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        _builder.Scene.DestroyEntity(lamp);
        _builder.Scene.History.Undo();

        Entity back = _builder.Scene.Find("Lamp")!.Value;
        World.Get<PointLight>(back).Range.Should().Be(7);
        World.Get<Transform>(back).Position.Should().Be(new Vector3(1, 0, 0));
        World.Get<EntityFolder>(back).Path.Should().Be("Lights");
        _builder.Scene.History.Redo();
        _builder.Scene.Find("Lamp").Should().BeNull();
    }

    [Fact]
    public void UndoesADuplicateAndEditsMadeToTheCopy()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        Entity copy = _builder.Scene.DuplicateEntity(lamp);
        Transform moved = new() { Position = new Vector3(9, 0, 0) };
        _builder.Scene.SetComponent(copy, moved);
        _builder.Scene.History.Undo();
        _builder.Scene.History.UndoName.Should().Be("Duplicate Lamp");
        _builder.Scene.History.Undo();

        _builder.Scene.Find("Lamp 2").Should().BeNull();
        _builder.Scene.History.Redo();
        _builder.Scene.History.Redo();
        Entity again = _builder.Scene.Find("Lamp 2")!.Value;
        World.Get<Transform>(again).Position.Should().Be(new Vector3(9, 0, 0));
    }

    [Fact]
    public void UndoesFiling()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        _builder.Scene.SetFolder(lamp, "Hall");
        _builder.Scene.History.Undo();

        World.Get<EntityFolder>(lamp).Path.Should().Be("Lights");
    }

    [Fact]
    public void ForgetsTheHistoryWhenAnotherSceneOpens()
    {
        Entity lamp = _builder.WithScene("Room.fvscene", Room).WithScene("Hall.fvscene", Room).Open("Room.fvscene");
        Transform moved = new() { Position = new Vector3(5, 0, 0) };
        _builder.Scene.SetComponent(lamp, moved);

        _builder.Scene.Open("Hall.fvscene");

        _builder.Scene.History.CanUndo.Should().BeFalse();
    }
}
