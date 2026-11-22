using System.Text.Json.Serialization;

using FlickerVoid.Gameplay.World.Objects.Doors;
using FlickerVoid.Gameplay.World.Society.Residents;

namespace FlickerVoid.Gameplay.Serialization;

/// <summary>Source-generated JSON for the game's scene components.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Protagonist.Protagonist))]
[JsonSerializable(typeof(DoorSwing))]
[JsonSerializable(typeof(Resident))]
public sealed partial class FlickerVoidJsonContext : JsonSerializerContext;
