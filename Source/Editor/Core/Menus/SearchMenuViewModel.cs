using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Menus;

/// <summary>
/// A menu with a search box: its rows show the choices grouped as the menu asks (<see cref="MenuGrouping"/>), or, while there is
/// search text, every choice that matches it in one list. The keys move <see cref="Selected"/> over the selectable rows and
/// <see cref="Activate"/> runs a choice or opens or leaves a group.
/// </summary>
public sealed partial class SearchMenuViewModel : ObservableObject
{
    private readonly IReadOnlyList<MenuChoice> _choices;
    private readonly MenuGrouping _grouping;

    public SearchMenuViewModel(IReadOnlyList<MenuChoice> choices, MenuGrouping grouping)
    {
        _choices = choices;
        _grouping = grouping;
        Rebuild();
    }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<MenuRow> Rows { get; private set; } = [];

    [ObservableProperty]
    public partial MenuRow? Selected { get; set; }

    /// <summary>The group whose choices the rows show, when the menu groups by submenus.</summary>
    public string? OpenGroup { get; private set; }

    /// <summary>Selects the next (<paramref name="step"/> 1) or previous (-1) selectable row, wrapping round.</summary>
    public void Move(int step)
    {
        List<MenuRow> selectable = [.. Rows.Where(row => row.IsSelectable)];
        if (selectable.Count == 0)
        {
            return;
        }

        int index = Selected is null ? -1 : selectable.IndexOf(Selected);
        if (index < 0 && step < 0)
        {
            index = 0;
        }

        int next = (index + step + selectable.Count) % selectable.Count;
        Selected = selectable[next];
    }

    /// <summary>Runs the row's choice, or opens or leaves its group; true when a choice ran, so the menu can close.</summary>
    public bool Activate(MenuRow? row)
    {
        switch (row?.Kind)
        {
            case MenuRowKind.Choice:
                row.Choice!.Run();
                return true;
            case MenuRowKind.Group:
                OpenGroup = row.Text;
                Rebuild();
                return false;
            case MenuRowKind.Back:
                OpenGroup = null;
                Rebuild();
                return false;
            default:
                return false;
        }
    }

    partial void OnSearchTextChanged(string value) => Rebuild();

    private void Rebuild()
    {
        string search = SearchText.Trim();
        List<MenuRow> rows = [];
        if (_grouping == MenuGrouping.Sections)
        {
            IEnumerable<MenuChoice> shown = search.Length > 0 ? _choices.Where(choice => choice.Matches(search)) : _choices.Where(choice => !choice.SearchOnly);
            AddSections(rows, shown);
        }
        else if (search.Length > 0)
        {
            foreach (MenuChoice choice in _choices.Where(choice => choice.Matches(search)))
            {
                rows.Add(ForChoice(choice, choice.Group));
            }
        }
        else if (_grouping == MenuGrouping.None)
        {
            rows.AddRange(_choices.Select(choice => ForChoice(choice, null)));
        }
        else if (_grouping == MenuGrouping.Separators)
        {
            AddSections(rows, _choices);
        }
        else
        {
            AddGroups(rows);
        }

        Rows = rows;
        Selected = rows.FirstOrDefault(row => row.IsSelectable);
    }

    private static void AddSections(List<MenuRow> rows, IEnumerable<MenuChoice> choices)
    {
        foreach (IGrouping<string?, MenuChoice> group in choices.GroupBy(choice => choice.Group))
        {
            if (group.Key is not null)
            {
                rows.Add(new MenuRow(MenuRowKind.Heading, group.Key));
            }

            rows.AddRange(group.Select(choice => ForChoice(choice, null)));
        }
    }

    private void AddGroups(List<MenuRow> rows)
    {
        if (OpenGroup is { } open)
        {
            rows.Add(new MenuRow(MenuRowKind.Back, open));
            rows.AddRange(_choices.Where(choice => choice.Group == open).Select(choice => ForChoice(choice, null)));
            return;
        }

        foreach (IGrouping<string?, MenuChoice> group in _choices.GroupBy(choice => choice.Group))
        {
            if (group.Key is null)
            {
                rows.AddRange(group.Select(choice => ForChoice(choice, null)));
            }
            else
            {
                rows.Add(new MenuRow(MenuRowKind.Group, group.Key));
            }
        }
    }

    private static MenuRow ForChoice(MenuChoice choice, string? detail)
    {
        return new MenuRow(MenuRowKind.Choice, choice.Name, choice.Icon, choice, detail ?? choice.Detail);
    }
}
