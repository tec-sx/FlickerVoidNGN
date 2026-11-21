using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Editor.Core.Inspector;

namespace FlickerVoid.Editor.Assets.Data;

/// <summary>
/// A data table in the Data tab: a form per row, generated from the row type like a data asset's, with rows added, renamed and
/// removed, and the whole table exported to and imported from CSV; every change is held unsaved.
/// </summary>
public sealed partial class TableViewModel
{
    private readonly TableDocument _document;
    private readonly FindContentFiles? _files;

    public TableViewModel(TableDocument document, FindContentFiles? files = null)
    {
        _document = document;
        _files = files;
        ShowRows();
    }

    public string Path => _document.Path;

    public ObservableCollection<TableRowViewModel> Rows { get; } = [];

    [RelayCommand]
    public void AddRow()
    {
        KeyValuePair<string, object> added = _document.AddRow("Row");
        TableRowViewModel row = CreateRow(added.Key, added.Value);
        Rows.Add(row);
        _document.Hold();
    }

    [RelayCommand]
    public void RemoveRow(TableRowViewModel row)
    {
        _document.RemoveRow(row.Name);
        Rows.Remove(row);
        _document.Hold();
    }

    public string ExportCsv()
    {
        return TableCsv.Export(_document.RowType, _document.Rows);
    }

    /// <summary>Replaces the rows with the CSV's; throws <see cref="InvalidDataException"/>, changing nothing, when it can't be read.</summary>
    public void ImportCsv(string csv)
    {
        List<KeyValuePair<string, object>> rows = TableCsv.Import(_document.RowType, csv);
        _document.ReplaceRows(rows);
        ShowRows();
        _document.Hold();
    }

    internal bool Rename(string name, string newName)
    {
        if (!_document.RenameRow(name, newName))
        {
            return false;
        }

        _document.Hold();
        return true;
    }

    private void ShowRows()
    {
        Rows.Clear();
        foreach ((string name, object value) in _document.Rows)
        {
            TableRowViewModel row = CreateRow(name, value);
            Rows.Add(row);
        }
    }

    private TableRowViewModel CreateRow(string name, object value)
    {
        DataObjectViewModel form = new(name, value, _document.RowType, _document.Hold, _files);
        return new TableRowViewModel(this, name, form);
    }
}
