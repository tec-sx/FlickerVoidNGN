using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Core.Data;

namespace FlickerVoid.Editor.Assets.Data;

/// <summary>One row of a data table: its name, renamed in place unless the name is empty or taken, and its form.</summary>
public sealed partial class TableRowViewModel : ObservableObject
{
    private bool _reverting;

    public TableRowViewModel(TableViewModel table, string name, DataObjectViewModel form)
    {
        Table = table;
        Form = form;
        _reverting = true;
        Name = name;
        _reverting = false;
    }

    public TableViewModel Table { get; }

    public DataObjectViewModel Form { get; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    partial void OnNameChanged(string oldValue, string newValue)
    {
        if (_reverting)
        {
            return;
        }

        if (Table.Rename(oldValue, newValue))
        {
            return;
        }

        _reverting = true;
        Name = oldValue;
        _reverting = false;
    }
}
