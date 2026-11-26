using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// A <c>Name.fvmat</c> file, a material instance: values for the parameters the scene shader declares, by name; a parameter it
/// leaves out keeps its parent's value (<see cref="MaterialChain"/>), or the shader's default without a parent.
/// </summary>
public sealed record MaterialFile
{
    public const string Extension = ".fvmat";

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    /// <summary>The master or material this one is an instance of, whose values it keeps where it sets none.</summary>
    [ContentFile(Extension, MasterFile.Extension)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Parent { get; set; }

    public Dictionary<string, JsonElement> Parameters { get; set; } = [];
}
