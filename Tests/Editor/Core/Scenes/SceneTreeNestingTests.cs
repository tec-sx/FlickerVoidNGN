using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class SceneTreeNestingTests : IDisposable
{
    private const string Stairs = """
        { "entities": [
          { "name": "Trigger", "folder": "Cellar", "components": { "transform": {} } },
          { "name": "Enter", "parent": "Trigger", "components": { "transform": {} } },
          { "name": "Exit", "parent": "Trigger", "components": { "transform": {} } },
          { "name": "Handle", "parent": "Enter", "components": { "transform": {} } },
          { "name": "Lamp", "components": { "transform": {} } }
        ] }
        """;

    private readonly EditorSceneTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    private Entity Named(string name) => _builder.Scene.Find(name)!.Value;

    private SceneTreeViewModel OpenTree()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Stairs.fvscene", Stairs).Open("Stairs.fvscene");
        return tree;
    }

    private static SceneTreeItem Trigger(SceneTreeViewModel tree) => tree.Items[0].Children[0];

    [Fact]
    public void ShowsOnlyTheTopEntitiesWithAButtonOnThoseWithNestedOnes()
    {
        SceneTreeViewModel tree = OpenTree();

        tree.Items.Select(item => item.Name).Should().Equal("Cellar", "Lamp");
        SceneTreeItem trigger = Trigger(tree);
        trigger.Name.Should().Be("Trigger");
        trigger.Children.Should().BeEmpty();
        trigger.HasNested.Should().BeTrue();
        tree.Items[1].HasNested.Should().BeFalse();
    }

    [Fact]
    public void RevealsEverythingNestedUnderAnEntityMarkedAsNestedAndConcealsItAgain()
    {
        SceneTreeViewModel tree = OpenTree();

        Trigger(tree).ToggleRevealCommand.Execute(null);

        SceneTreeItem trigger = Trigger(tree);
        trigger.IsRevealed.Should().BeTrue();
        trigger.Children.Select(item => item.Name).Should().Equal("Enter", "Exit");
        trigger.Children.Should().OnlyContain(item => item.IsNested);
        trigger.Children[0].Children.Select(item => item.Name).Should().Equal("Handle");
        trigger.ToggleRevealCommand.Execute(null);
        Trigger(tree).Children.Should().BeEmpty();
    }

    [Fact]
    public void RevealsEveryNestedEntityAtOnce()
    {
        SceneTreeViewModel tree = OpenTree();

        tree.RevealAllNested = true;

        Trigger(tree).Children.Should().HaveCount(2);
        tree.RevealAllNested = false;
        Trigger(tree).Children.Should().BeEmpty();
    }

    [Fact]
    public void HidesAnEntityFromItsRowAndShowsItAgain()
    {
        SceneTreeViewModel tree = OpenTree();

        Trigger(tree).ToggleHiddenCommand.Execute(null);

        Trigger(tree).IsHidden.Should().BeTrue();
        _builder.Scene.IsHidden(Named("Handle")).Should().BeTrue();
        Trigger(tree).ToggleHiddenCommand.Execute(null);
        _builder.Scene.IsHidden(Named("Trigger")).Should().BeFalse();
    }

    [Fact]
    public void HidesAndShowsTheSelectionAndShowsEverything()
    {
        SceneTreeViewModel tree = OpenTree();
        _builder.Scene.Selection.SelectAll([Named("Lamp")], Named("Lamp"));

        tree.HideSelectedCommand.Execute(null);
        tree.Items[1].IsHidden.Should().BeTrue();
        tree.ShowSelectedCommand.Execute(null);
        tree.Items[1].IsHidden.Should().BeFalse();
        _builder.Scene.SetHidden([Named("Trigger"), Named("Lamp")], hidden: true);
        tree.ShowAllCommand.Execute(null);

        _builder.Scene.Entities.Should().NotContain(entity => _builder.Scene.IsHidden(entity));
        tree.Items[0].IsHidden.Should().BeFalse();
    }

    [Fact]
    public void NestsDroppedEntitiesUnderTheEntityTheyLandOn()
    {
        SceneTreeViewModel tree = OpenTree();

        tree.Nest([Named("Lamp")], Named("Exit"));

        _builder.Scene.GetParent(Named("Lamp")).Should().Be(Named("Exit"));
        tree.Items.Select(item => item.Name).Should().Equal("Cellar");
    }

    [Fact]
    public void FilingAnEntityInAFolderTakesItOffItsParent()
    {
        SceneTreeViewModel tree = OpenTree();

        tree.MoveTo([Named("Enter")], "Cellar");

        _builder.Scene.GetParent(Named("Enter")).Should().BeNull();
        tree.Items[0].Children.Select(item => item.Name).Should().Equal("Trigger", "Enter");
    }

    [Fact]
    public void ShowsANestedSelectionUnderItsRevealedParent()
    {
        SceneTreeViewModel tree = OpenTree();
        Trigger(tree).ToggleRevealCommand.Execute(null);

        _builder.Scene.Selection.Select(Named("Handle"));

        tree.SelectedItems.Select(item => item.Name).Should().Equal("Handle");
    }

    [Fact]
    public void TheEntityTabShowsTheWholeTreeOfTheSelectedEntity()
    {
        OpenTree();
        EntityTreeViewModel entityTree = new(_builder.Scene);

        _builder.Scene.Selection.Select(Named("Handle"));

        entityTree.Items.Select(item => item.Name).Should().Equal("Trigger");
        SceneTreeItem root = entityTree.Items[0];
        root.Children.Select(item => item.Name).Should().Equal("Enter", "Exit");
        root.Children[0].Children.Select(item => item.Name).Should().Equal("Handle");
        root.Children[0].Children[0].IsNested.Should().BeFalse();
        entityTree.SelectedItems.Select(item => item.Name).Should().Equal("Handle");
        entityTree.Title.Should().Be("Trigger");
    }

    [Fact]
    public void TheEntityTabIsEmptyWithNothingSelected()
    {
        OpenTree();
        EntityTreeViewModel entityTree = new(_builder.Scene);

        _builder.Scene.Selection.Select(null);

        entityTree.Items.Should().BeEmpty();
    }

    [Fact]
    public void ChoosingInTheEntityTabSelectsTheEntity()
    {
        OpenTree();
        EntityTreeViewModel entityTree = new(_builder.Scene);
        _builder.Scene.Selection.Select(Named("Trigger"));

        SceneTreeItem exit = entityTree.Items[0].Children[1];
        entityTree.Choose([exit]);

        _builder.Scene.Selection.Selected.Should().Be(Named("Exit"));
        entityTree.Items[0].Name.Should().Be("Trigger");
    }

    [Fact]
    public void RevealsWhatANestedEntitySelectedElsewhereIsNestedInAndShowsItSelected()
    {
        SceneTreeViewModel tree = OpenTree();
        tree.Items[0].IsExpanded = false;

        _builder.Scene.Selection.Select(Named("Handle"));

        tree.Items[0].IsExpanded.Should().BeTrue("the Cellar folder holds the selection");
        SceneTreeItem trigger = Trigger(tree);
        trigger.IsRevealed.Should().BeTrue();
        trigger.Children[0].IsExpanded.Should().BeTrue();
        tree.SelectedItems.Select(item => item.Name).Should().Equal("Handle");
    }

    [Fact]
    public void ExpandsTheFolderOfATopEntitySelectedElsewhere()
    {
        SceneTreeViewModel tree = OpenTree();
        tree.Items[0].IsExpanded = false;

        _builder.Scene.Selection.Select(Named("Trigger"));

        tree.Items[0].IsExpanded.Should().BeTrue();
        Trigger(tree).IsRevealed.Should().BeFalse("nothing nested is selected");
        tree.SelectedItems.Select(item => item.Name).Should().Equal("Trigger");
    }
}
