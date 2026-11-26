using Avalonia.Data.Converters;

using FlickerVoid.Editor.Core.Menus;

namespace FlickerVoid.Editor.Controls;

/// <summary>Turns a view model's choices into a menu's items, grouped as the menu that shows them asks, and a group's name into its heading.</summary>
public static class MenuConverters
{
    public static FuncValueConverter<IEnumerable<MenuChoice>?, IReadOnlyList<MenuNode>> Plain { get; } = new(choices => Build(choices, MenuGrouping.None));

    public static FuncValueConverter<IEnumerable<MenuChoice>?, IReadOnlyList<MenuNode>> Separators { get; } = new(choices => Build(choices, MenuGrouping.Separators));

    public static FuncValueConverter<IEnumerable<MenuChoice>?, IReadOnlyList<MenuNode>> Submenus { get; } = new(choices => Build(choices, MenuGrouping.Submenus));

    /// <summary>A group's name as a heading shows it, in capitals.</summary>
    public static FuncValueConverter<string?, string> Upper { get; } = new(static text => text?.ToUpperInvariant() ?? "");

    private static IReadOnlyList<MenuNode> Build(IEnumerable<MenuChoice>? choices, MenuGrouping grouping)
    {
        if (choices is null)
        {
            return [];
        }

        return MenuNode.Build(choices, grouping);
    }
}
