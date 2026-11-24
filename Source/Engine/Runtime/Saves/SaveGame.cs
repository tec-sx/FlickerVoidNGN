using System.Text.Json.Nodes;

namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>One saved game: its metadata, its scene, the facts, the state of each saveable entity there and in the scenes left, and what modules write into it.</summary>
public sealed class SaveGame
{
    public const int CurrentVersion = 1;

    public SaveMetadata Metadata { get; set; } = new();

    /// <summary>The scene that was loaded, which a load travels to first; empty when the game had no scene travel.</summary>
    public string Scene { get; set; } = string.Empty;

    /// <summary>Every fact by its tag's name.</summary>
    public Dictionary<string, int> Facts { get; set; } = [];

    /// <summary>The state of each saveable entity by its id, then by the key of each <see cref="SaveState"/> it has.</summary>
    public Dictionary<string, Dictionary<string, JsonNode>> Entities { get; set; } = [];

    /// <summary>The ids of the saveable entities that were gone.</summary>
    public List<string> Destroyed { get; set; } = [];

    /// <summary>The scenes visited and left, by their paths, as they were left.</summary>
    public Dictionary<string, SavedScene> Scenes { get; set; } = [];

    /// <summary>Game-wide state modules keep by their own keys, such as the map's waypoint.</summary>
    public Dictionary<string, JsonNode> States { get; set; } = [];
}
