using System.Text.Json.Serialization;

using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Input.Gestures;

namespace FlickerVoid.Engine.Platform.Serialization;

/// <summary>Source-generated JSON for input maps and gestures, keys and buttons by name.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(InputMap))]
[JsonSerializable(typeof(Gesture))]
public sealed partial class PlatformJsonContext : JsonSerializerContext;
