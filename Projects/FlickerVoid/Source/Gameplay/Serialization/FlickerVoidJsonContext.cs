using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Gameplay.Protagonist.Control;
using FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;
using FlickerVoid.Gameplay.World.Objects.Doors;
using FlickerVoid.Gameplay.World.Society.Residents;

namespace FlickerVoid.Gameplay.Serialization;

/// <summary>Source-generated JSON for the game's scene components, data assets and what saves keep.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, Converters = [typeof(TagJsonConverter), typeof(QuaternionJsonConverter), typeof(Vector3JsonConverter)])]
[JsonSerializable(typeof(Protagonist.Protagonist))]
[JsonSerializable(typeof(DoorSwing))]
[JsonSerializable(typeof(DoorHinge))]
[JsonSerializable(typeof(Resident))]
[JsonSerializable(typeof(Scanner))]
[JsonSerializable(typeof(Scannable))]
[JsonSerializable(typeof(ScanDefinition))]
[JsonSerializable(typeof(SavedScannable))]
[JsonSerializable(typeof(InteractableHighlight))]
public sealed partial class FlickerVoidJsonContext : JsonSerializerContext;
