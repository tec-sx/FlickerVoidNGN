using System.Collections;
using System.Collections.Specialized;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using FlickerVoid.Editor.Core.Lists;

namespace FlickerVoid.Editor.Controls.Lists;

/// <summary>
/// A list that selects several rows by the rules of <see cref="ListSelection{T}"/>: a click selects one, Ctrl adds or takes out,
/// Shift spans from the anchor over the shown rows, Ctrl+A selects all, arrows move and Shift arrows grow, and a right click
/// outside the selection selects that row first, so the row menu acts on the whole selection (<see cref="SelectedRows"/>).
/// </summary>
public class ItemList : ListBox
{
    private readonly ListSelection<object> _selection = new();
    private bool _syncing;

    public ItemList()
    {
        SelectionMode = SelectionMode.Multiple;
        AddHandler(PointerPressedEvent, HandlePointerPressed, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        _selection.Changed += ShowSelection;
        SelectionChanged += HandleSelectionChanged;
    }

    /// <summary>The selected rows in the order shown.</summary>
    public IReadOnlyList<object> SelectedRows => _selection.Selected;

    protected override Type StyleKeyOverride => typeof(ListBox);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ItemsSourceProperty)
        {
            return;
        }

        if (change.OldValue is INotifyCollectionChanged old)
        {
            old.CollectionChanged -= HandleRowsChanged;
        }

        if (change.NewValue is INotifyCollectionChanged rows)
        {
            rows.CollectionChanged += HandleRowsChanged;
        }

        ReadRows();
    }

    /// <summary>Takes a selection the list made itself (bound from a view model) into the rules.</summary>
    private void HandleSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        _selection.Clear();
        foreach (object? row in SelectedItems ?? Array.Empty<object>())
        {
            if (row is not null)
            {
                _selection.Click(row, control: true);
            }
        }

        _syncing = false;
    }

    private void HandleRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ReadRows();

    private void ReadRows()
    {
        List<object> rows = [];
        if (ItemsSource is IEnumerable source)
        {
            foreach (object? row in source)
            {
                if (row is not null)
                {
                    rows.Add(row);
                }
            }
        }

        _selection.Show(rows);
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual source || source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { } container)
        {
            return;
        }

        object? row = container.DataContext;
        if (row is null)
        {
            return;
        }

        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed)
        {
            _selection.RightClick(row);
            container.Focus();
            e.Handled = true;
            return;
        }

        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        bool control = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _selection.Click(row, control, shift);
        container.Focus();
        e.Handled = true;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        bool control = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.A when control:
                _selection.SelectAll();
                e.Handled = true;
                break;
            case Key.Up:
                _selection.MoveFocus(-1, shift);
                e.Handled = true;
                break;
            case Key.Down:
                _selection.MoveFocus(1, shift);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Shows the rules' selection on the list.</summary>
    private void ShowSelection()
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        Selection.BeginBatchUpdate();
        Selection.Clear();
        foreach (object row in _selection.Selected)
        {
            int index = Items.IndexOf(row);
            if (index >= 0)
            {
                Selection.Select(index);
            }
        }

        Selection.EndBatchUpdate();
        _syncing = false;
        if (_selection.Focus is { } focus)
        {
            ScrollIntoView(focus);
        }
    }
}
