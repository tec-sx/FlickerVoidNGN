using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Editor.Tests.Scenes;

public sealed class EditorSceneTests : IDisposable
{
    private readonly EditorSceneTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SavesTheEditedWorldIntoTheOpenSceneFile()
    {
        Entity lamp = _builder.WithScene("Scenes/Room.scene.json", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""").Open("Scenes/Room.scene.json");
        Vector3 moved = new(2, 1, 0);
        _builder.Scene.Game.World.Get<Transform>(lamp).Position = moved;

        bool saved = _builder.Scene.Save();

        saved.Should().BeTrue();
        _builder.ReadScene("Scenes/Room.scene.json").Should().Contain("\"position\":[2,1,0]");
        _builder.Scene.Game.World.Get<Transform>(lamp).Position.Should().Be(moved);
    }

    [Fact]
    public void CannotSaveAWorldWithoutASceneFile()
    {
        bool saved = _builder.Scene.Save();

        saved.Should().BeFalse();
    }
}
