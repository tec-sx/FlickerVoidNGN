using FlickerVoid.Editor.Core.Menus;

namespace FlickerVoid.Editor.Core.Tests.Menus;

public sealed class SearchMenuViewModelTests
{
    private readonly MenuTestsBuilder _builder = new();

    [Fact]
    public void ShowsHeadingsOverEachGroupWithSeparators()
    {
        SearchMenuViewModel menu = new(_builder.AssetChoices(), MenuGrouping.Separators);

        menu.Rows.Select(row => $"{row.Kind}:{row.Text}").Should().Equal(
            "Choice:Folder", "Choice:Scene", "Heading:Look", "Choice:Material", "Choice:Mood", "Heading:Flow", "Choice:Flow Graph");
        menu.Selected!.Text.Should().Be("Folder");
    }

    [Fact]
    public void OpensAndLeavesGroupsWithSubmenus()
    {
        SearchMenuViewModel menu = new(_builder.AssetChoices(), MenuGrouping.Submenus);
        menu.Rows.Select(row => row.Text).Should().Equal("Folder", "Scene", "Look", "Flow");

        bool closed = menu.Activate(menu.Rows[2]);

        closed.Should().BeFalse();
        menu.Rows.Select(row => $"{row.Kind}:{row.Text}").Should().Equal("Back:Look", "Choice:Material", "Choice:Mood");
        menu.Activate(menu.Rows[0]);
        menu.Rows.Should().HaveCount(4);
    }

    [Fact]
    public void FindsChoicesByNameOrGroupInOneList()
    {
        SearchMenuViewModel menu = new(_builder.AssetChoices(), MenuGrouping.Submenus);

        menu.SearchText = "look";

        menu.Rows.Select(row => row.Text).Should().Equal("Material", "Mood");
        menu.Rows[0].Detail.Should().Be("Look");
        menu.Selected.Should().Be(menu.Rows[0]);
    }

    [Fact]
    public void MovesOverSelectableRowsAndWrapsRound()
    {
        SearchMenuViewModel menu = new(_builder.AssetChoices(), MenuGrouping.Separators);

        menu.Move(1);
        menu.Move(1);
        menu.Selected!.Text.Should().Be("Material");
        menu.Move(-1);
        menu.Move(-1);
        menu.Move(-1);
        menu.Selected!.Text.Should().Be("Flow Graph");
    }

    [Fact]
    public void RunsTheActivatedChoiceAndAsksToClose()
    {
        SearchMenuViewModel menu = new(_builder.AssetChoices(), MenuGrouping.None);
        menu.SearchText = "scene";

        bool closed = menu.Activate(menu.Selected);

        closed.Should().BeTrue();
        _builder.Ran.Should().Equal("Scene");
    }
}
