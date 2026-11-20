using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>An entity template file (<c>.fventity</c>): components with their values, over those of an optional base template.</summary>
public sealed class EntityTemplateFile
{
    /// <summary>The schema the file names for text editors; the game ignores it.</summary>
    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    /// <summary>The template this one changes, relative to the content folder.</summary>
    public string? Base { get; set; }

    public Dictionary<string, JsonElement> Components { get; set; } = [];
}
