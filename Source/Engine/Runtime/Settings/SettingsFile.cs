using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Debugging;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Settings;

/// <summary>
/// A project's settings: one JSON object naming console variables and the values they start with, only those that differ from their
/// defaults, so removing a line puts a variable back to its default. Reading never throws: a malformed file, an unknown name or a bad
/// value is reported and skipped, so a hand edit can always put back a setting that broke the program.
/// </summary>
public static class SettingsFile
{
    public const string Extension = ".fvsettings";

    /// <summary>The settings file's name, at the root of a project's content.</summary>
    public const string FileName = "Settings" + Extension;

    /// <summary>The name of the schema of settings files among <see cref="ContentSchemas"/>.</summary>
    public const string SchemaName = "settings";

    private static readonly JsonDocumentOptions ReadOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
    private static readonly JsonWriterOptions WriteOptions = new() { Indented = true };

    /// <summary>Puts the console's saved variables back to their defaults, then sets those the JSON names; returns what it skipped and why.</summary>
    public static IReadOnlyList<string> Apply(string json, IDebugConsole console)
    {
        List<ConsoleVariable> variables = FindSaved(console);
        foreach (ConsoleVariable variable in variables)
        {
            variable.Reset();
        }

        List<string> problems = [];
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, ReadOptions);
        }
        catch (JsonException exception)
        {
            problems.Add($"The settings are not valid JSON: {exception.Message}");
            return problems;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                problems.Add("The settings must be one JSON object.");
                return problems;
            }

            Dictionary<string, ConsoleVariable> byName = variables.ToDictionary(variable => variable.Name, StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (property.Name == JsonSchemas.SchemaProperty)
                {
                    continue;
                }

                if (!byName.TryGetValue(property.Name, out ConsoleVariable? variable))
                {
                    problems.Add($"Unknown setting '{property.Name}'.");
                    continue;
                }

                string? text = ReadText(property.Value);
                if (text is null)
                {
                    problems.Add($"{variable.Name} must be a boolean, a number or a string.");
                    continue;
                }

                if (!variable.TrySet(text))
                {
                    string invalid = variable.DescribeInvalid(text);
                    problems.Add(invalid);
                }
            }
        }

        return problems;
    }

    /// <summary>Applies the file, or only puts the saved variables back to their defaults when there is no file.</summary>
    public static IReadOnlyList<string> Load(string path, IDebugConsole console)
    {
        string json = "{}";
        string? unread = null;
        try
        {
            if (File.Exists(path))
            {
                json = File.ReadAllText(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            unread = $"The settings could not be read: {exception.Message}";
        }

        IReadOnlyList<string> problems = Apply(json, console);
        if (unread is null)
        {
            return problems;
        }

        return [unread, .. problems];
    }

    /// <summary>The console's saved variables that differ from their defaults, by name, after the schema the file names.</summary>
    public static string Write(IDebugConsole console, string schema)
    {
        List<ConsoleVariable> variables = FindSaved(console);
        IEnumerable<ConsoleVariable> changed = variables.Where(variable => !variable.IsDefault);
        IOrderedEnumerable<ConsoleVariable> sorted = changed.OrderBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, WriteOptions))
        {
            writer.WriteStartObject();
            writer.WriteString(JsonSchemas.SchemaProperty, schema);
            foreach (ConsoleVariable variable in sorted)
            {
                writer.WritePropertyName(variable.Name);
                WriteValue(writer, variable.Kind, variable.Text);
            }

            writer.WriteEndObject();
        }

        string json = Encoding.UTF8.GetString(stream.ToArray());
        return json + Environment.NewLine;
    }

    /// <summary>Writes the file at the root of a content folder, naming the schema a development build writes beside it.</summary>
    public static void Save(string path, IDebugConsole console)
    {
        string schema = $"{ContentSchemas.FolderName}/{SchemaName}.schema.json";
        string json = Write(console, schema);
        File.WriteAllText(path, json);
    }

    /// <summary>The schema of settings files: every saved variable the console has, with its help, kind, choices and default.</summary>
    public static JsonObject CreateSchema(IDebugConsole console)
    {
        JsonObject properties = new() { [JsonSchemas.SchemaProperty] = new JsonObject { ["type"] = "string" } };
        List<ConsoleVariable> variables = FindSaved(console);
        foreach (ConsoleVariable variable in variables)
        {
            JsonObject property = new() { ["description"] = variable.Help };
            switch (variable.Kind)
            {
                case ConsoleValueKind.Boolean:
                    property["type"] = "boolean";
                    property["default"] = variable.DefaultText == "true";
                    break;
                case ConsoleValueKind.Number:
                    property["type"] = "number";
                    property["default"] = JsonNode.Parse(variable.DefaultText);
                    break;
                default:
                    property["type"] = "string";
                    if (variable.Choices.Count > 0)
                    {
                        JsonArray choices = [.. variable.Choices.Select(choice => (JsonNode?)choice)];
                        property["enum"] = choices;
                    }

                    property["default"] = variable.DefaultText;
                    break;
            }

            properties[variable.Name] = property;
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };
    }

    private static List<ConsoleVariable> FindSaved(IDebugConsole console)
    {
        IEnumerable<ConsoleVariable> variables = console.Entries.OfType<ConsoleVariable>();
        return [.. variables.Where(variable => variable.Saved)];
    }

    private static string? ReadText(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Number:
                return value.GetRawText();
            case JsonValueKind.String:
                return value.GetString();
            default:
                return null;
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, ConsoleValueKind kind, string text)
    {
        switch (kind)
        {
            case ConsoleValueKind.Boolean:
                writer.WriteBooleanValue(text == "true");
                break;
            case ConsoleValueKind.Number:
                writer.WriteRawValue(text);
                break;
            default:
                writer.WriteStringValue(text);
                break;
        }
    }
}
