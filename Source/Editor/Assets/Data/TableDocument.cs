using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Runtime.Data;

namespace FlickerVoid.Editor.Assets.Data;

/// <summary>
/// A data table file being edited: its rows, in order and by name, edited in place, added, renamed and removed;
/// <see cref="ContentDocument{T}.Hold"/> keeps them unsaved in the content until they are saved or discarded. Throws
/// <see cref="InvalidDataException"/> when the file holds no registered row type or can't be read.
/// </summary>
public sealed class TableDocument : ContentDocument<TableRows>
{
    public TableDocument(UnsavedContent content, DataTypes types, string path)
        : base(content, path, file => ReadRows(types, path, file), (table, schema) => types.WriteTable(table.RowType, table.Rows, schema))
    {
        RowType = types.Options.GetTypeInfo(Value.RowType);
    }

    public JsonTypeInfo RowType { get; }

    public IReadOnlyList<KeyValuePair<string, object>> Rows => Value.Rows;

    /// <summary>Adds a row with the type's default values, named <paramref name="name"/> or that name numbered when it is taken.</summary>
    public KeyValuePair<string, object> AddRow(string name)
    {
        string free = FindFreeName(name);
        object row = RowType.CreateObject!();
        KeyValuePair<string, object> added = new(free, row);
        Value.Rows.Add(added);
        return added;
    }

    public void RemoveRow(string name)
    {
        int index = IndexOf(name);
        Value.Rows.RemoveAt(index);
    }

    /// <summary>Renames a row in place; false, with nothing changed, when the new name is empty or taken.</summary>
    public bool RenameRow(string name, string newName)
    {
        if (newName.Length == 0 || IndexOf(newName) >= 0)
        {
            return false;
        }

        int index = IndexOf(name);
        Value.Rows[index] = new KeyValuePair<string, object>(newName, Value.Rows[index].Value);
        return true;
    }

    /// <summary>Replaces every row, as a CSV import does.</summary>
    public void ReplaceRows(IEnumerable<KeyValuePair<string, object>> rows)
    {
        Value.Rows.Clear();
        Value.Rows.AddRange(rows);
    }

    private string FindFreeName(string name)
    {
        string candidate = name;
        int number = 2;
        while (IndexOf(candidate) >= 0)
        {
            candidate = $"{name} {number}";
            number++;
        }

        return candidate;
    }

    private static TableRows ReadRows(DataTypes types, string path, JsonObject file)
    {
        List<KeyValuePair<string, object>> rows = types.ReadRows(path, file, out Type rowType);
        return new TableRows(rowType, rows);
    }

    private int IndexOf(string name)
    {
        return Value.Rows.FindIndex(row => row.Key == name);
    }
}
