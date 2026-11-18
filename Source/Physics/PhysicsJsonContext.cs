using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Physics;

/// <summary>Source-generated JSON for physics components, with the same rules as the runtime's files.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    AllowOutOfOrderMetadataProperties = true,
    Converters = [typeof(Vector3JsonConverter)])]
[JsonSerializable(typeof(RigidBody))]
[JsonSerializable(typeof(StaticBody))]
public sealed partial class PhysicsJsonContext : JsonSerializerContext;
