using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// A <c>Name.fvmat</c> file: values for the parameters the scene shader declares, by name; a parameter it leaves
/// out keeps the shader's default.
/// </summary>
public sealed record MaterialFile
{
    public const string Extension = ".fvmat";

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    public Dictionary<string, JsonElement> Parameters { get; init; } = [];
}
