using System.Text.Json.Serialization;

using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Characters.Serialization;

/// <summary>Source-generated JSON for the character components scenes hold, and the movement saves keep.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, Converters = [typeof(Vector3JsonConverter)])]
[JsonSerializable(typeof(CharacterMover))]
[JsonSerializable(typeof(Destination))]
[JsonSerializable(typeof(Facing))]
public sealed partial class CharactersJsonContext : JsonSerializerContext;
