using System.Text.Json.Nodes;

using Arch.Core;

namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>
/// One kind of state a module saves for each saveable entity: <see cref="Write"/> gives it, or null when the entity has none, and
/// <see cref="Read"/> puts it back, given null when the save holds none for the entity.
/// </summary>
public sealed record SaveState(string Key, Func<Entity, JsonNode?> Write, Action<Entity, JsonNode?> Read);
