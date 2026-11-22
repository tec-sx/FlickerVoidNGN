using FlickerVoid.Editor.Core.Menus;

namespace FlickerVoid.Editor.Core.Tests.Menus;

public sealed class MenuNodeTests
{
    private readonly MenuTestsBuilder _builder = new();

    [Fact]
    public void ListsChoicesInOrderWithoutGrouping()
    {
        IReadOnlyList<MenuNode> nodes = MenuNode.Build(_builder.AssetChoices(), MenuGrouping.None);

        nodes.Select(node => node.Header).Should().Equal("Folder", "Scene", "Material", "Mood", "Flow Graph");
    }

    [Fact]
    public void PutsSeparatorsBetweenGroups()
    {
        IReadOnlyList<MenuNode> nodes = MenuNode.Build(_builder.AssetChoices(), MenuGrouping.Separators);

        nodes.Select(node => node.Header).Should().Equal("Folder", "Scene", "-", "Material", "Mood", "-", "Flow Graph");
        nodes[2].IsSeparator.Should().BeTrue();
    }

    [Fact]
    public void MakesASubmenuPerGroupAndKeepsUngroupedChoicesAtTheTop()
    {
        IReadOnlyList<MenuNode> nodes = MenuNode.Build(_builder.AssetChoices(), MenuGrouping.Submenus);

        nodes.Select(node => node.Header).Should().Equal("Folder", "Scene", "Look", "Flow");
        nodes[2].Children.Select(node => node.Header).Should().Equal("Material", "Mood");
        nodes[2].Command.Should().BeNull();
    }

    [Fact]
    public void RunsTheChoiceThroughItsCommand()
    {
        IReadOnlyList<MenuNode> nodes = MenuNode.Build(_builder.AssetChoices(), MenuGrouping.Submenus);

        nodes[3].Children[0].Command!.Execute(null);

        _builder.Ran.Should().Equal("Flow Graph");
    }
}
