using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;
using FlickerVoid.Gameplay.World.Objects.Doors;
using FlickerVoid.Gameplay.World.Society.Residents;

namespace FlickerVoid.Gameplay.Serialization;

/// <summary>Source-generated JSON for the game's scene components and data assets.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, Converters = [typeof(TagJsonConverter)])]
[JsonSerializable(typeof(Protagonist.Protagonist))]
[JsonSerializable(typeof(DoorSwing))]
[JsonSerializable(typeof(Resident))]
[JsonSerializable(typeof(Scanner))]
[JsonSerializable(typeof(Scannable))]
[JsonSerializable(typeof(ScanDefinition))]
public sealed partial class FlickerVoidJsonContext : JsonSerializerContext;
