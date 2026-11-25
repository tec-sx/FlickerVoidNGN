using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>Source-generated JSON for save files and the runtime's own saved state.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SaveGame))]
[JsonSerializable(typeof(SavedRandom))]
[JsonSerializable(typeof(SavedClock))]
internal sealed partial class SavesJsonContext : JsonSerializerContext;
