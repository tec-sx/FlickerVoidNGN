using Arch.Core;

using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Cameras.Triggers;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class EditorSceneRenameTests : IDisposable
{
    private const string Inn = """
        { "entities": [
          { "name": "Fox", "components": { "transform": {}, "cameraTarget": {} } },
          { "name": "House", "components": { "transform": {} } },
          { "name": "Door", "parent": "House", "components": { "transform": {} } },
          { "name": "Room Shot", "components": { "transform": {}, "shot": { "keys": [ { "position": [0, 0, 0] } ], "target": "Fox" } } },
          { "name": "Window Spot", "components": { "transform": {}, "triggerArea": { "size": [1, 1, 1] }, "cameraCue": { "shot": "Room Shot" } } }
        ] }
        """;

    private readonly EditorSceneTestsBuilder _builder = new([new CamerasModule()]);

    public EditorSceneRenameTests()
    {
        _builder.WithScene("Inn.fvscene", Inn).Open("Inn.fvscene");
    }

    public void Dispose() => _builder.Dispose();

    private World World => _builder.Scene.Game.World;

    private Entity Named(string name) => _builder.Scene.Find(name)!.Value;

    [Fact]
    public void RenamingAnEntityKeepsWhatIsNestedUnderItThere()
    {
        bool renamed = _builder.Scene.RenameEntity(Named("House"), "Inn");

        renamed.Should().BeTrue();
        _builder.Scene.GetParent(Named("Door")).Should().Be(Named("Inn"));
        _builder.Scene.IsUnsaved.Should().BeTrue();
    }

    [Fact]
    public void RenamingAnEntityRewritesTheReferencesThatNameIt()
    {
        _builder.Scene.RenameEntity(Named("Fox"), "Hero");
        _builder.Scene.RenameEntity(Named("Room Shot"), "Inn Shot");
        _builder.Scene.Save();

        World.Get<Shot>(Named("Inn Shot")).Target.Should().Be("Hero");
        World.Get<CameraCue>(Named("Window Spot")).Shot.Should().Be("Inn Shot");
        _builder.ReadScene("Inn.fvscene").Should().Contain("\"target\":\"Hero\"").And.Contain("\"shot\":\"Inn Shot\"");
    }

    [Fact]
    public void UndoingARenameNamesEverythingBack()
    {
        _builder.Scene.RenameEntity(Named("Room Shot"), "Inn Shot");

        _builder.Scene.History.Undo();

        _builder.Scene.Find("Inn Shot").Should().BeNull();
        World.Get<CameraCue>(Named("Window Spot")).Shot.Should().Be("Room Shot");
        _builder.Scene.History.Redo();
        World.Get<CameraCue>(Named("Window Spot")).Shot.Should().Be("Inn Shot");
    }

    [Theory]
    [InlineData("Door")]
    [InlineData("")]
    [InlineData("   ")]
    public void RefusesANameAnotherEntityHasOrNone(string name)
    {
        bool renamed = _builder.Scene.RenameEntity(Named("House"), name);

        renamed.Should().BeFalse();
        World.Get<EntityName>(Named("House")).Value.Should().Be("House");
        _builder.Scene.History.CanUndo.Should().BeFalse();
    }
}
