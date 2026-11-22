using System.Text.Json.Nodes;

namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>Game-wide state a module saves: <see cref="Write"/> gives it, or null for nothing, and <see cref="Read"/> puts it back when a save holds it.</summary>
public sealed record GlobalSaveState(string Key, Func<JsonNode?> Write, Action<JsonNode?> Read);
