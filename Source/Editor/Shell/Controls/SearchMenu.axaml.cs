using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using FlickerVoid.Editor.Core.Menus;

namespace FlickerVoid.Editor.Controls;

/// <summary>
/// The editor's searchable menu, as Shift+A opens it over the viewport, the flow graph and the asset browser: a search box over
/// the menu's rows, which scroll past the menu's height. Keys typed anywhere in it go to the search box.
/// </summary>
public sealed partial class SearchMenu : UserControl
{
    private readonly SearchBox _search;
    private readonly ListBox _rows;
    private Flyout? _flyout;

    public SearchMenu()
    {
        AvaloniaXamlLoader.Load(this);
        _search = this.GetControl<SearchBox>("Search");
        _rows = this.GetControl<ListBox>("Rows");
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        _rows.AddHandler(PointerReleasedEvent, HandleRowReleased, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private SearchMenuViewModel? Menu => DataContext as SearchMenuViewModel;

    /// <summary>Opens a menu of <paramref name="choices"/> at the pointer over <paramref name="anchor"/>, grouped as asked.</summary>
    public static void Show(Control anchor, IReadOnlyList<MenuChoice> choices, MenuGrouping grouping, bool atPointer = true)
    {
        SearchMenuViewModel menu = new(choices, grouping);
        SearchMenu view = new() { DataContext = menu };
        Flyout flyout = new() { Content = view, Placement = atPointer ? PlacementMode.Pointer : PlacementMode.Center, FlyoutPresenterClasses = { "searchMenu" } };
        view._flyout = flyout;
        flyout.Opened += (_, _) => view._search.Focus();
        flyout.ShowAt(anchor, showAtPointer: atPointer);
    }

    /// <summary>Opens the command palette under the top of <paramref name="window"/>: the choices under their groups' headings.</summary>
    public static void ShowPalette(Control window, IReadOnlyList<MenuChoice> choices)
    {
        SearchMenuViewModel menu = new(choices, MenuGrouping.Sections);
        SearchMenu view = new() { DataContext = menu, Classes = { "palette" } };
        view._search.PlaceholderText = "Search or run a command";
        Flyout flyout = new()
        {
            Content = view,
            Placement = PlacementMode.AnchorAndGravity,
            PlacementAnchor = Avalonia.Controls.Primitives.PopupPositioning.PopupAnchor.Top,
            PlacementGravity = Avalonia.Controls.Primitives.PopupPositioning.PopupGravity.Bottom,
            VerticalOffset = PaletteTop,
            FlyoutPresenterClasses = { "searchMenu" },
        };
        view._flyout = flyout;
        flyout.Opened += (_, _) => view._search.Focus();
        flyout.ShowAt(window);
    }

    /// <summary>How far under the window's top the palette opens.</summary>
    private const double PaletteTop = 72;

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (Menu is not { } menu)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                menu.Move(1);
                ScrollToSelected();
                e.Handled = true;
                break;
            case Key.Up:
                menu.Move(-1);
                ScrollToSelected();
                e.Handled = true;
                break;
            case Key.Enter:
                Activate(menu.Selected);
                e.Handled = true;
                break;
            case Key.Escape when string.IsNullOrEmpty(menu.SearchText):
                _flyout?.Hide();
                e.Handled = true;
                break;
            default:
                if (!_search.IsFocused)
                {
                    _search.Focus();
                }

                break;
        }
    }

    private void HandleRowReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        StyledElement? source = e.Source as StyledElement;
        if (source?.DataContext is MenuRow row)
        {
            Activate(row);
        }
    }

    private void Activate(MenuRow? row)
    {
        if (Menu is not { } menu)
        {
            return;
        }

        bool ran = menu.Activate(row);
        if (ran)
        {
            _flyout?.Hide();
        }
        else
        {
            _search.Focus();
        }
    }

    private void ScrollToSelected()
    {
        if (Menu?.Selected is { } selected)
        {
            _rows.ScrollIntoView(selected);
        }
    }
}
