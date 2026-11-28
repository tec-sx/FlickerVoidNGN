namespace FlickerVoid.Editor.Core.Lists;

/// <summary>
/// The selected rows of a list that selects several: a click selects one, Ctrl adds or takes out, Shift spans from the anchor over
/// the shown rows, arrows move with Shift growing, a right click outside the selection selects that row, and a filter drops the
/// rows it hides. Raises <see cref="Changed"/> when the selection changes.
/// </summary>
public sealed class ListSelection<T>
    where T : notnull
{
    private readonly List<T> _shown = [];
    private readonly HashSet<T> _selected = [];
    private T? _anchor;
    private T? _focus;

    /// <summary>The selected rows in the order shown.</summary>
    public IReadOnlyList<T> Selected => _shown.Where(_selected.Contains).ToList();

    public IReadOnlyList<T> Shown => _shown;

    /// <summary>The row the arrows move from, or the default when none.</summary>
    public T? Focus => _focus;

    public event Action? Changed;

    public bool IsSelected(T item) => _selected.Contains(item);

    /// <summary>Takes the rows shown now, in order, and drops the selected rows no longer among them.</summary>
    public void Show(IEnumerable<T> rows)
    {
        _shown.Clear();
        _shown.AddRange(rows);
        HashSet<T> shown = [.. _shown];
        int dropped = _selected.RemoveWhere(item => !shown.Contains(item));
        if (_anchor is not null && !shown.Contains(_anchor))
        {
            _anchor = default;
        }

        if (_focus is not null && !shown.Contains(_focus))
        {
            _focus = default;
        }

        if (dropped > 0)
        {
            Changed?.Invoke();
        }
    }

    public void Click(T item, bool control = false, bool shift = false)
    {
        HashSet<T> before = [.. _selected];
        if (shift)
        {
            if (!control)
            {
                _selected.Clear();
            }

            SelectSpan(_anchor ?? _shown.FirstOrDefault(), item);
            _focus = item;
        }
        else if (control)
        {
            if (!_selected.Remove(item))
            {
                _selected.Add(item);
            }

            _anchor = item;
            _focus = item;
        }
        else
        {
            SelectOnly(item);
        }

        TellIfChanged(before);
    }

    /// <summary>Selects only the row, unless it is selected already, so the menu acts on the whole selection.</summary>
    public void RightClick(T item)
    {
        if (_selected.Contains(item))
        {
            _focus = item;
            return;
        }

        HashSet<T> before = [.. _selected];
        SelectOnly(item);
        TellIfChanged(before);
    }

    public void SelectAll()
    {
        HashSet<T> before = [.. _selected];
        _selected.UnionWith(_shown);
        TellIfChanged(before);
    }

    public void Clear()
    {
        HashSet<T> before = [.. _selected];
        _selected.Clear();
        _anchor = default;
        _focus = default;
        TellIfChanged(before);
    }

    /// <summary>Moves the focus by rows, stopping at the ends; it selects only the new row, or spans from the anchor with Shift.</summary>
    public void MoveFocus(int rows, bool shift = false)
    {
        if (_shown.Count == 0)
        {
            return;
        }

        int index = -1;
        if (_focus is not null)
        {
            index = _shown.IndexOf(_focus);
        }

        int next = Math.Clamp(index + rows, 0, _shown.Count - 1);
        T item = _shown[next];
        if (shift)
        {
            Click(item, shift: true);
            return;
        }

        HashSet<T> before = [.. _selected];
        SelectOnly(item);
        TellIfChanged(before);
    }

    private void SelectOnly(T item)
    {
        _selected.Clear();
        _selected.Add(item);
        _anchor = item;
        _focus = item;
    }

    private void SelectSpan(T? from, T to)
    {
        int end = _shown.IndexOf(to);
        int start = end;
        if (from is not null)
        {
            int found = _shown.IndexOf(from);
            if (found >= 0)
            {
                start = found;
            }
        }

        if (end < 0)
        {
            return;
        }

        int low = Math.Min(start, end);
        int high = Math.Max(start, end);
        for (int index = low; index <= high; index++)
        {
            _selected.Add(_shown[index]);
        }

        _anchor ??= from;
    }

    private void TellIfChanged(HashSet<T> before)
    {
        if (!before.SetEquals(_selected))
        {
            Changed?.Invoke();
        }
    }
}
