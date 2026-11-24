using System.Text.Json.Nodes;

namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>What was kept of a scene the game left: the state of each saveable entity by its id, and the ids of those that were gone.</summary>
public sealed class SavedScene
{
    public Dictionary<string, Dictionary<string, JsonNode>> Entities { get; set; } = [];

    public List<string> Destroyed { get; set; } = [];
}
