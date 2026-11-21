using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Gameplay.Protagonist.Control;
using FlickerVoid.Gameplay.World.Objects.Doors;

namespace FlickerVoid.Gameplay.Serialization;

/// <summary>Source-generated JSON for the game's scene components.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, Converters = [typeof(Vector3JsonConverter)])]
[JsonSerializable(typeof(Protagonist.Protagonist))]
[JsonSerializable(typeof(DoorSwing))]
[JsonSerializable(typeof(InteractableHighlight))]
public sealed partial class FlickerVoidJsonContext : JsonSerializerContext;
