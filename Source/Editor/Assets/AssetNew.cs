using System.Text;

namespace FlickerVoid.Editor.Assets;

/// <summary>
/// What New writes of an asset type: the menu entry, the new file's name before its number and extension, the schema the file
/// names, and its text given that schema's path from the file's folder.
/// </summary>
public sealed record AssetNew(string Label, string FileName, string Schema, Func<string, string> Write)
{
    /// <summary>The New menu's group the entry sits in; null for none.</summary>
    public string? Group { get; init; }

    /// <summary>Writes a JSON object naming its schema, followed by the members <paramref name="body"/> holds, if any.</summary>
    public static Func<string, string> Json(string? body)
    {
        return schemaPath =>
        {
            StringBuilder text = new();
            text.Append("{\n");
            text.Append($"  \"$schema\": \"{schemaPath}\"");
            if (body is not null)
            {
                text.Append($",\n  {body}");
            }

            text.Append("\n}\n");
            return text.ToString();
        };
    }
}
