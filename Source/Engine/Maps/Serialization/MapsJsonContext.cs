using System.Text.Json.Serialization;

using FlickerVoid.Engine.Maps.Definitions;
using FlickerVoid.Engine.Maps.Markers;
using FlickerVoid.Engine.Maps.Navigation;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Maps.Serialization;

/// <summary>Source-generated JSON for map and marker definitions, their fragments and the components scenes hold.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true,
    Converters = [typeof(TagJsonConverter), typeof(Vector2JsonConverter)])]
[JsonSerializable(typeof(MapDefinition))]
[JsonSerializable(typeof(MarkerDefinition))]
[JsonSerializable(typeof(MinimapFragment))]
[JsonSerializable(typeof(CompassFragment))]
[JsonSerializable(typeof(WorldMapFragment))]
[JsonSerializable(typeof(DiscoveryFragment))]
[JsonSerializable(typeof(MapMarker))]
[JsonSerializable(typeof(Navigator))]
public sealed partial class MapsJsonContext : JsonSerializerContext;
