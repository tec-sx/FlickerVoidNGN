using System.Text.Json.Serialization;

using FlickerVoid.Gameplay.World.Objects.Doors;

namespace FlickerVoid.Gameplay.Serialization;

/// <summary>Source-generated JSON for the game's scene components.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Protagonist.Protagonist))]
[JsonSerializable(typeof(DoorSwing))]
public sealed partial class FlickerVoidJsonContext : JsonSerializerContext;
