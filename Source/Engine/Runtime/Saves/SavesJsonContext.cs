using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>Source-generated JSON for save files.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SaveGame))]
internal sealed partial class SavesJsonContext : JsonSerializerContext;
