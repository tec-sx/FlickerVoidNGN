using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>
/// One entity in a scene file: its name, the entity template it starts from, if any, and its components, each keyed by the name it
/// is registered under. With a template, the components hold only what differs from it.
/// </summary>
public sealed class EntityData
{
    public string Name { get; set; } = "";

    /// <summary>The <c>.fventity</c> file, relative to the content folder, whose components this entity starts with.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Template { get; set; }

    public Dictionary<string, JsonElement> Components { get; set; } = [];
}
