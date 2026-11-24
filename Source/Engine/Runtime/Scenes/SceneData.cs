using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>A scene file: the entities it spawns.</summary>
public sealed class SceneData
{
    public const string Extension = ".fvscene";

    /// <summary>The schema the file names for text editors; the game ignores it.</summary>
    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    public List<EntityData> Entities { get; set; } = [];
}
