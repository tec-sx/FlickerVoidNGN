using System.Text.Json.Nodes;

namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>One saved game: its metadata, the facts, the state of each saveable entity and what modules write into it.</summary>
public sealed class SaveGame
{
    public const int CurrentVersion = 1;

    public SaveMetadata Metadata { get; set; } = new();

    /// <summary>Every fact by its tag's name.</summary>
    public Dictionary<string, int> Facts { get; set; } = [];

    /// <summary>The state of each saveable entity by its id, then by the key of each <see cref="SaveState"/> it has.</summary>
    public Dictionary<string, Dictionary<string, JsonNode>> Entities { get; set; } = [];

    /// <summary>The ids of the saveable entities that were gone.</summary>
    public List<string> Destroyed { get; set; } = [];

    /// <summary>Game-wide state modules keep by their own keys, such as the map's waypoint.</summary>
    public Dictionary<string, JsonNode> States { get; set; } = [];
}
