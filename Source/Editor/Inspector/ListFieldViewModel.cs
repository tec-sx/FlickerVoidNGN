using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>A string array property, such as a material per mesh part: an editable line per item, added and removed one by one; an empty optional list is null.</summary>
public sealed partial class ListFieldViewModel(IEditableValue property, IReadOnlyList<string> suggestions) : FieldViewModel(property)
{
    private bool _showing;

    public ObservableCollection<ListItemViewModel> Items { get; } = [];

    [RelayCommand]
    public void Add()
    {
        ListItemViewModel item = new(this, "", suggestions);
        Items.Add(item);
        WriteItems();
    }

    [RelayCommand]
    public void Remove(ListItemViewModel? item)
    {
        if (item is not null && Items.Remove(item))
        {
            WriteItems();
        }
    }

    internal void WriteItems()
    {
        if (_showing)
        {
            return;
        }

        if (Items.Count == 0 && Property.AcceptsNull)
        {
            Write(null);
            return;
        }

        string[] values = [.. Items.Select(static item => item.Text)];
        Write(values);
    }

    /// <summary>Rebuilds the items only when the world's list differs, so a refresh doesn't take the focus from an item being typed in.</summary>
    protected override void Show(object? value)
    {
        string[] values = value as string[] ?? [];
        bool same = values.SequenceEqual(Items.Select(static item => item.Text));
        if (same)
        {
            return;
        }

        _showing = true;
        try
        {
            Items.Clear();
            foreach (string text in values)
            {
                ListItemViewModel item = new(this, text, suggestions);
                Items.Add(item);
            }
        }
        finally
        {
            _showing = false;
        }
    }
}
