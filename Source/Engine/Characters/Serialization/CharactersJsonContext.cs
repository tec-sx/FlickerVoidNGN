using System.Text.Json.Serialization;

using FlickerVoid.Engine.Characters.Movement;

namespace FlickerVoid.Engine.Characters.Serialization;

/// <summary>Source-generated JSON for the character components scenes hold.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CharacterMover))]
public sealed partial class CharactersJsonContext : JsonSerializerContext;
