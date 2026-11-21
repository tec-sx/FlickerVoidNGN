using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;

using FlickerVoid.Editor.Core.Menus;

namespace FlickerVoid.Editor.Controls;

/// <summary>
/// The editor's two menus, alike everywhere: the Add menu, a <see cref="SearchMenu"/> that Shift+A opens over any control given
/// <see cref="AddChoicesProperty"/>, grouped as <see cref="AddGroupingProperty"/> says; and the options menu a right-click opens, a
/// plain context menu of choices without search.
/// </summary>
public static class EditorMenus
{
    public static readonly AttachedProperty<IReadOnlyList<MenuChoice>?> AddChoicesProperty =
        AvaloniaProperty.RegisterAttached<Control, IReadOnlyList<MenuChoice>?>("AddChoices", typeof(EditorMenus));

    public static readonly AttachedProperty<MenuGrouping> AddGroupingProperty =
        AvaloniaProperty.RegisterAttached<Control, MenuGrouping>("AddGrouping", typeof(EditorMenus), MenuGrouping.Separators);

    /// <summary>The key that opens the Add menu, also shown beside the options menus' Add entries.</summary>
    public const string AddGesture = "Shift+A";

    static EditorMenus()
    {
        AddChoicesProperty.Changed.AddClassHandler<Control>(HandleAddChoicesChanged);
    }

    public static IReadOnlyList<MenuChoice>? GetAddChoices(Control control) => control.GetValue(AddChoicesProperty);

    public static void SetAddChoices(Control control, IReadOnlyList<MenuChoice>? value) => control.SetValue(AddChoicesProperty, value);

    public static MenuGrouping GetAddGrouping(Control control) => control.GetValue(AddGroupingProperty);

    public static void SetAddGrouping(Control control, MenuGrouping value) => control.SetValue(AddGroupingProperty, value);

    /// <summary>Opens the control's Add menu at the pointer, if it has choices.</summary>
    public static bool ShowAdd(Control control)
    {
        IReadOnlyList<MenuChoice>? choices = GetAddChoices(control);
        if (choices is null || choices.Count == 0)
        {
            return false;
        }

        MenuGrouping grouping = GetAddGrouping(control);
        SearchMenu.Show(control, choices, grouping);
        return true;
    }

    /// <summary>The options menu's entry that opens the control's Add menu.</summary>
    public static MenuChoice AddEntry(Control control, string name = "Add…")
    {
        return new MenuChoice(name, () => ShowAdd(control), Icon: "Add", Gesture: AddGesture);
    }

    /// <summary>Opens a context menu of <paramref name="items"/> at the pointer over <paramref name="anchor"/>.</summary>
    public static void ShowOptions(Control anchor, IReadOnlyList<MenuNode> items)
    {
        ContextMenu menu = new() { ItemsSource = items };
        if (anchor.TryFindResource("ChoiceMenuItem", out object? theme) && theme is ControlTheme itemTheme)
        {
            menu.ItemContainerTheme = itemTheme;
        }

        menu.Open(anchor);
    }

    /// <summary>Opens a context menu of <paramref name="choices"/>, grouped as asked.</summary>
    public static void ShowOptions(Control anchor, IEnumerable<MenuChoice> choices, MenuGrouping grouping)
    {
        IReadOnlyList<MenuNode> items = MenuNode.Build(choices, grouping);
        ShowOptions(anchor, items);
    }

    private static void HandleAddChoicesChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.RemoveHandler(InputElement.KeyDownEvent, HandleKeyDown);
        if (e.NewValue is not null)
        {
            control.AddHandler(InputElement.KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        }
    }

    /// <summary>Shift+A anywhere in the control but in a text field.</summary>
    private static void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.A || e.KeyModifiers != KeyModifiers.Shift || e.Source is TextBox || sender is not Control control)
        {
            return;
        }

        e.Handled = ShowAdd(control);
    }
}
