using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Vehicles.Serialization;

/// <summary>Source-generated JSON for vehicle files and the vehicle component: comments and trailing commas allowed, unknown properties rejected.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    UseStringEnumConverter = true,
    Converters = [typeof(Vector3JsonConverter), typeof(QuaternionJsonConverter)])]
[JsonSerializable(typeof(VehicleFile))]
[JsonSerializable(typeof(Vehicle))]
public sealed partial class VehiclesJsonContext : JsonSerializerContext;
