using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Tests.Projects;

public sealed class ScenePlayerTests : IDisposable
{
    private const string Room = """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""";

    private readonly ScenePlayerTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void PlaysTheWorldAsItIsNowWithoutSavingTheScene()
    {
        Entity lamp = _builder.Scenes.WithScene("Scenes/Room.fvscene", Room).Open("Scenes/Room.fvscene");
        _builder.Scenes.Scene.Game.World.Get<Transform>(lamp).Position = new Vector3(2, 1, 0);

        bool played = _builder.Build().Play();

        played.Should().BeTrue();
        string[] expected = ["--scene", ".play/Room.fvscene"];
        _builder.Launcher.Received(1).Launch(ScenePlayerTestsBuilder.Program, Arg.Is<IReadOnlyList<string>>(arguments => arguments.SequenceEqual(expected)));
        _builder.Scenes.ReadScene(".play/Room.fvscene").Should().Contain("\"position\":[2,1,0]");
        _builder.Scenes.ReadScene("Scenes/Room.fvscene").Should().Be(Room);
    }

    [Fact]
    public void CannotPlayAWorldWithoutASceneFile()
    {
        bool played = _builder.Build().Play();

        played.Should().BeFalse();
        _builder.Launcher.DidNotReceiveWithAnyArgs().Launch(default!, default!);
    }
}
