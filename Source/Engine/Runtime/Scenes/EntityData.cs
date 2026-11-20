using System.Text.Json;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>One entity in a scene file: its name and its components, each keyed by the name it is registered under.</summary>
public sealed class EntityData
{
    public string Name { get; set; } = "";

    public Dictionary<string, JsonElement> Components { get; set; } = [];
}
