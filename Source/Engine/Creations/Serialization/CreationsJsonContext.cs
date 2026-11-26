using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Creations.Serialization;

/// <summary>Source-generated JSON for what saves keep of creations' parts.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SavedPartToggles))]
public sealed partial class CreationsJsonContext : JsonSerializerContext;
