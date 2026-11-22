using System.Text.Json.Serialization;

using FlickerVoid.Engine.Ai.Senses;

namespace FlickerVoid.Engine.Ai.Serialization;

/// <summary>Source-generated JSON for the AI components scenes hold.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Perception))]
[JsonSerializable(typeof(Perceivable))]
public sealed partial class AiJsonContext : JsonSerializerContext;
