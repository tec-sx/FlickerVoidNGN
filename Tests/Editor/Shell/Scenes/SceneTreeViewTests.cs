using Avalonia.Controls;

using FlickerVoid.Editor.Core.Scenes;

namespace FlickerVoid.Editor.Tests.Scenes;

public sealed class SceneTreeViewTests : IDisposable
{
    private readonly SceneTreeViewTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public async Task KeepsASelectionMadeElsewhereWhenTheEntityTabShowsAnotherTree()
    {
        await _builder.Run(() =>
        {
            _builder.Scene.Selection.Select(_builder.Named("Inn"));
            SceneTreeViewTestsBuilder.Settle();

            _builder.Scene.Selection.Select(_builder.Named("House"));
            SceneTreeViewTestsBuilder.Settle();

            _builder.Scene.Selection.Selected.Should().Be(_builder.Named("House"));
            SceneTreeViewTestsBuilder.SelectedNames(_builder.Outline).Should().Equal("House");
            SceneTreeViewTestsBuilder.SelectedNames(_builder.EntityTab).Should().Equal("House");
        });
    }

    [Fact]
    public async Task KeepsAChoiceInTheOutlineWhenTheEntityTabShowsAnotherTree()
    {
        await _builder.Run(() =>
        {
            _builder.Scene.Selection.Select(_builder.Named("Inn"));
            SceneTreeViewTestsBuilder.Settle();
            TreeView tree = _builder.Outline.GetControl<TreeView>("Tree");
            SceneTreeItem house = ((SceneTreeViewModel)_builder.Outline.DataContext!).Items[0].Children[1];

            tree.SelectedItems.Clear();
            tree.SelectedItems.Add(house);
            SceneTreeViewTestsBuilder.Settle();

            _builder.Scene.Selection.Selected.Should().Be(_builder.Named("House"));
            SceneTreeViewTestsBuilder.SelectedNames(_builder.EntityTab).Should().Equal("House");
        });
    }

    [Fact]
    public async Task KeepsTheSelectionWhenTheOutlineIsRebuilt()
    {
        await _builder.Run(() =>
        {
            _builder.Scene.Selection.Select(_builder.Named("Lamp"));
            SceneTreeViewTestsBuilder.Settle();

            _builder.Scene.SetFolder(_builder.Named("House"), "Houses");
            SceneTreeViewTestsBuilder.Settle();

            _builder.Scene.Selection.Selected.Should().Be(_builder.Named("Lamp"));
            SceneTreeViewTestsBuilder.SelectedNames(_builder.Outline).Should().Equal("Lamp");
        });
    }

    [Fact]
    public async Task ShowsANestedEntitySelectedInTheViewportInTheOutline()
    {
        await _builder.Run(() =>
        {
            _builder.Scene.Selection.Select(_builder.Named("Inn Door"));
            SceneTreeViewTestsBuilder.Settle();

            _builder.Scene.Selection.Selected.Should().Be(_builder.Named("Inn Door"));
            SceneTreeViewTestsBuilder.SelectedNames(_builder.Outline).Should().Equal("Inn Door");
        });
    }
}
