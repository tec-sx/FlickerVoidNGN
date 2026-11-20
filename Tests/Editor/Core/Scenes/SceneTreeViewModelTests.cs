using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class SceneTreeViewModelTests : IDisposable
{
    private readonly EditorSceneTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ListsTheOpenScenesEntitiesAndFollowsTheSelection()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        Entity lamp = _builder
            .WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } }, { "name": "Box", "components": { "transform": {} } } ] }""")
            .Open("Room.fvscene");

        _builder.Scene.Selection.Select(lamp);

        tree.Items.Select(item => item.Name).Should().Equal("Lamp", "Box");
        tree.SelectedItem!.Entity.Should().Be(lamp);
    }

    [Fact]
    public void ChoosingAnItemSelectsItsEntity()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder
            .WithScene("Room.fvscene", """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } }, { "name": "Box", "components": { "transform": {} } } ] }""")
            .Open("Room.fvscene");

        tree.SelectedItem = tree.Items[1];

        _builder.Scene.Selection.Selected.Should().Be(tree.Items[1].Entity);
    }
}
