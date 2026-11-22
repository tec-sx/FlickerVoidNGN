using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Editor.Core.Menus;

/// <summary>An item of a built menu: a choice, a submenu of a group's choices or a separator (<see cref="IsSeparator"/>).</summary>
public sealed class MenuNode
{
    /// <summary>The header Avalonia draws as a separator.</summary>
    public const string SeparatorHeader = "-";

    private MenuNode(string header, string? icon, string? gesture, ICommand? command, IReadOnlyList<MenuNode> children)
    {
        Header = header;
        Icon = icon;
        Gesture = gesture;
        Command = command;
        Children = children;
    }

    public string Header { get; }

    public string? Icon { get; }

    public string? Gesture { get; }

    public ICommand? Command { get; }

    public IReadOnlyList<MenuNode> Children { get; }

    public bool IsSeparator => Header == SeparatorHeader;

    public static MenuNode Separator { get; } = new(SeparatorHeader, null, null, null, []);

    public static MenuNode ForChoice(MenuChoice choice)
    {
        RelayCommand command = new(choice.Run);
        return new MenuNode(choice.Name, choice.Icon, choice.Gesture, command, []);
    }

    public static MenuNode ForGroup(string name, IReadOnlyList<MenuNode> children)
    {
        return new MenuNode(name, null, null, null, children);
    }

    /// <summary>The items of a menu of <paramref name="choices"/>, grouped by their groups in the order they first appear.</summary>
    public static IReadOnlyList<MenuNode> Build(IEnumerable<MenuChoice> choices, MenuGrouping grouping)
    {
        if (grouping == MenuGrouping.None)
        {
            return [.. choices.Select(ForChoice)];
        }

        List<MenuNode> nodes = [];
        foreach (IGrouping<string?, MenuChoice> group in choices.GroupBy(choice => choice.Group))
        {
            MenuNode[] members = [.. group.Select(ForChoice)];
            if (grouping == MenuGrouping.Separators)
            {
                if (nodes.Count > 0)
                {
                    nodes.Add(Separator);
                }

                nodes.AddRange(members);
            }
            else if (group.Key is null)
            {
                nodes.AddRange(members);
            }
            else
            {
                nodes.Add(ForGroup(group.Key, members));
            }
        }

        return nodes;
    }
}
