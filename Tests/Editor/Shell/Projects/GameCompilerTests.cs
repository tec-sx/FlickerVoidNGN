using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Projects;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Editor.Tests.Projects;

public sealed class GameCompilerTests : IDisposable
{
    private const string Room = """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""";

    private readonly GameCompilerTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public async Task BuildsTheGameAndReloadsTheSceneWithItsCodeKeepingUnsavedEdits()
    {
        Entity lamp = _builder.Scenes.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scenes.Scene;
        Game before = scene.Game;
        scene.SetComponent(lamp, new Transform { Position = new Vector3(2, 1, 0) });
        GameCompiler compiler = _builder.Build();

        await compiler.Compile();

        _builder.Builder.Received(1).Build(GameCompilerTestsBuilder.GameProject);
        scene.Game.Should().NotBeSameAs(before);
        Entity reloaded = scene.Find("Lamp")!.Value;
        scene.Game.World.Get<Transform>(reloaded).Position.Should().Be(new Vector3(2, 1, 0));
        scene.IsUnsaved.Should().BeTrue();
    }

    [Fact]
    public async Task ReplacesTheCodeItReloadedLast()
    {
        GameCompiler compiler = _builder.Build();
        await compiler.Compile();
        GameCode? first = compiler.Code;

        await compiler.Compile();

        first.Should().NotBeNull();
        compiler.Code.Should().NotBeNull().And.NotBeSameAs(first);
    }

    [Fact]
    public async Task LeavesTheSceneAsItWasWhenTheBuildFails()
    {
        _builder.Scenes.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        EditorScene scene = _builder.Scenes.Scene;
        Game before = scene.Game;
        _builder.Builder.Build(GameCompilerTestsBuilder.GameProject).Returns(_ => throw new ProjectBuildException("Door.cs(3,1): error CS1002: ; expected"));
        GameCompiler compiler = _builder.Build();

        Func<Task> compile = compiler.Compile;

        await compile.Should().ThrowAsync<ProjectBuildException>();
        scene.Game.Should().BeSameAs(before);
        compiler.Code.Should().BeNull();
    }
}
