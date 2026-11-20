using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace FlickerVoid.Editor.Assets.Data;

/// <summary>
/// Data table rows as CSV, for spreadsheet work: a <c>name</c> column, then a column per property of the row type. Strings are
/// written as they are, other values as their JSON (numbers, true, [1, 2, 3]).
/// </summary>
public static class TableCsv
{
    public const string NameColumn = "name";

    public static string Export(JsonTypeInfo rowType, IEnumerable<KeyValuePair<string, object>> rows)
    {
        List<JsonPropertyInfo> columns = GetColumns(rowType);
        StringBuilder text = new();
        List<string> header = [NameColumn, .. columns.Select(static column => column.Name)];
        AppendLine(text, header);
        foreach ((string name, object row) in rows)
        {
            JsonObject values = JsonSerializer.SerializeToNode(row, rowType)!.AsObject();
            List<string> cells = [name];
            foreach (JsonPropertyInfo column in columns)
            {
                JsonNode? value = values[column.Name];
                cells.Add(ToCell(value));
            }

            AppendLine(text, cells);
        }

        return text.ToString();
    }

    /// <summary>The rows in the CSV; throws <see cref="InvalidDataException"/> for an unknown column or a value of the wrong type.</summary>
    public static List<KeyValuePair<string, object>> Import(JsonTypeInfo rowType, string csv)
    {
        List<List<string>> lines = Parse(csv);
        List<KeyValuePair<string, object>> rows = [];
        if (lines.Count == 0)
        {
            return rows;
        }

        List<string> header = lines[0];
        if (header.Count == 0 || header[0] != NameColumn)
        {
            throw new InvalidDataException($"The first column must be \"{NameColumn}\".");
        }

        Dictionary<string, JsonPropertyInfo> columns = GetColumns(rowType).ToDictionary(static column => column.Name);
        for (int index = 1; index < header.Count; index++)
        {
            if (!columns.ContainsKey(header[index]))
            {
                throw new InvalidDataException($"{rowType.Type.Name} has no property {header[index]}.");
            }
        }

        foreach (List<string> cells in lines.Skip(1))
        {
            if (cells.Count == 0 || cells.All(static cell => cell.Length == 0))
            {
                continue;
            }

            JsonObject values = [];
            for (int index = 1; index < header.Count && index < cells.Count; index++)
            {
                JsonPropertyInfo column = columns[header[index]];
                values[column.Name] = ToNode(column, cells[index]);
            }

            object row;
            try
            {
                row = values.Deserialize(rowType)!;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Row {cells[0]} can't be read: {exception.Message}", exception);
            }

            rows.Add(new KeyValuePair<string, object>(cells[0], row));
        }

        return rows;
    }

    private static List<JsonPropertyInfo> GetColumns(JsonTypeInfo rowType)
    {
        return [.. rowType.Properties.Where(static property => property.Get is not null && property.Set is not null)];
    }

    private static string ToCell(JsonNode? value)
    {
        if (value is JsonValue text && text.TryGetValue(out string? content))
        {
            return content;
        }

        return value?.ToJsonString() ?? "";
    }

    private static JsonNode? ToNode(JsonPropertyInfo column, string cell)
    {
        if (column.PropertyType == typeof(string))
        {
            return JsonValue.Create(cell);
        }

        if (cell.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(cell);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{cell} is not a value for {column.Name}.", exception);
        }
    }

    private static void AppendLine(StringBuilder text, IEnumerable<string> cells)
    {
        bool first = true;
        foreach (string cell in cells)
        {
            if (!first)
            {
                text.Append(',');
            }

            first = false;
            if (cell.Contains(',') || cell.Contains('"') || cell.Contains('\n'))
            {
                string escaped = cell.Replace("\"", "\"\"");
                text.Append('"').Append(escaped).Append('"');
            }
            else
            {
                text.Append(cell);
            }
        }

        text.Append('\n');
    }

    /// <summary>Splits CSV into lines of cells: commas, quoted cells with doubled quotes and line breaks inside quotes.</summary>
    private static List<List<string>> Parse(string csv)
    {
        List<List<string>> lines = [];
        List<string> cells = [];
        StringBuilder cell = new();
        bool quoted = false;
        for (int index = 0; index < csv.Length; index++)
        {
            char character = csv[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < csv.Length && csv[index + 1] == '"')
                {
                    cell.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    cell.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    cells.Add(cell.ToString());
                    cell.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    cells.Add(cell.ToString());
                    cell.Clear();
                    lines.Add(cells);
                    cells = [];
                    break;
                default:
                    cell.Append(character);
                    break;
            }
        }

        if (cell.Length > 0 || cells.Count > 0)
        {
            cells.Add(cell.ToString());
            lines.Add(cells);
        }

        return lines;
    }
}
