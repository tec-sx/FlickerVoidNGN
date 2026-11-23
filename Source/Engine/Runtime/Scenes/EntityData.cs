using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>
/// One entity in a scene file: its name, the entity template it starts from, if any, its outline folder, the entity it is nested under
/// and its components, each keyed by the name it is registered under. With a template, the components hold only what differs from it.
/// </summary>
public sealed class EntityData
{
    public string Name { get; set; } = "";

    /// <summary>The <c>.fventity</c> file, relative to the content folder, whose components this entity starts with.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Template { get; set; }

    /// <summary>The folder the editor's scene outline files the entity under, such as <c>Lights/Hall</c>; null at the top.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Folder { get; set; }

    /// <summary>The name of the entity this one is nested under, whose moves it follows; null at the top. Transforms stay in world space.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Parent { get; set; }

    public Dictionary<string, JsonElement> Components { get; set; } = [];
}
