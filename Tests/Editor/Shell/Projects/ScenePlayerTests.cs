using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Rendering.Serialization;
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

        bool played = _builder.Build().Play(new LightingMood());

        played.Should().BeTrue();
        string[] expected = ["--scene", ".play/Room.fvscene", "--mood", ".play/Room.fvmood"];
        _builder.Launcher.Received(1).Launch(ScenePlayerTestsBuilder.Program, Arg.Is<IReadOnlyList<string>>(arguments => arguments.SequenceEqual(expected)));
        _builder.Scenes.ReadScene(".play/Room.fvscene").Should().Contain("\"position\":[2,1,0]");
        _builder.Scenes.ReadScene("Scenes/Room.fvscene").Should().Be(Room);
    }

    [Fact]
    public void PlaysWithTheMoodTheViewportShows()
    {
        _builder.Scenes.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        LightingMood mood = new() { Exposure = 2.5f, Tonemapper = Tonemapper.Aces };

        _builder.Build().Play(mood);

        string written = _builder.Scenes.ReadScene(".play/Room.fvmood");
        LightingMood played = JsonSerializer.Deserialize(written, RenderingJsonContext.Default.LightingMood)!;
        played.Should().Be(mood);
    }

    [Fact]
    public void PlaysTheScenesOwnMoodAndEnvironmentWhenGivenNoMood()
    {
        _builder.Scenes.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        _builder.Build().Play(null);

        string[] expected = ["--scene", ".play/Room.fvscene"];
        _builder.Launcher.Received(1).Launch(ScenePlayerTestsBuilder.Program, Arg.Is<IReadOnlyList<string>>(arguments => arguments.SequenceEqual(expected)));
    }

    [Fact]
    public void CannotPlayAWorldWithoutASceneFile()
    {
        bool played = _builder.Build().Play(new LightingMood());

        played.Should().BeFalse();
        _builder.Launcher.DidNotReceiveWithAnyArgs().Launch(default!, default!);
    }
}
