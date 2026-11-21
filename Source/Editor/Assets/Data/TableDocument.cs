using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Editor.Assets.Data;

/// <summary>
/// A data table file being edited: its rows, in order and by name, edited in place, added, renamed and removed; <see cref="Hold"/>
/// keeps them unsaved in the content until they are saved or discarded.
/// </summary>
public sealed class TableDocument
{
    private readonly UnsavedContent _content;
    private readonly DataTypes _types;
    private readonly string? _schema;
    private readonly List<KeyValuePair<string, object>> _rows;

    /// <summary>Reads the file; throws <see cref="InvalidDataException"/> when it holds no registered row type or can't be read.</summary>
    public TableDocument(UnsavedContent content, DataTypes types, string path)
    {
        _content = content;
        _types = types;
        Path = path;
        _content.Unload(path);
        JsonObject file = _content.Load(path, RuntimeJsonContext.Default.JsonObject);
        _content.Unload(path);
        _schema = file[JsonSchemas.SchemaProperty]?.GetValue<string>();
        _rows = types.ReadRows(path, file, out Type rowType);
        RowType = types.Options.GetTypeInfo(rowType);
    }

    public string Path { get; }

    public JsonTypeInfo RowType { get; }

    public IReadOnlyList<KeyValuePair<string, object>> Rows => _rows;

    /// <summary>Adds a row with the type's default values, named <paramref name="name"/> or that name numbered when it is taken.</summary>
    public KeyValuePair<string, object> AddRow(string name)
    {
        string free = FindFreeName(name);
        object row = RowType.CreateObject!();
        KeyValuePair<string, object> added = new(free, row);
        _rows.Add(added);
        return added;
    }

    public void RemoveRow(string name)
    {
        int index = IndexOf(name);
        _rows.RemoveAt(index);
    }

    /// <summary>Renames a row in place; false, with nothing changed, when the new name is empty or taken.</summary>
    public bool RenameRow(string name, string newName)
    {
        if (newName.Length == 0 || IndexOf(newName) >= 0)
        {
            return false;
        }

        int index = IndexOf(name);
        _rows[index] = new KeyValuePair<string, object>(newName, _rows[index].Value);
        return true;
    }

    /// <summary>Replaces every row, as a CSV import does.</summary>
    public void ReplaceRows(IEnumerable<KeyValuePair<string, object>> rows)
    {
        _rows.Clear();
        _rows.AddRange(rows);
    }

    /// <summary>Keeps the rows as the file's unsaved contents.</summary>
    public void Hold()
    {
        byte[] contents = _types.WriteTable(RowType.Type, _rows, _schema);
        _content.Edit(Path, contents);
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

    private int IndexOf(string name)
    {
        return _rows.FindIndex(row => row.Key == name);
    }
}
