using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Props.Serialization;

/// <summary>Source-generated JSON for prop files and the prop component: comments and trailing commas allowed, unknown properties rejected.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    UseStringEnumConverter = true,
    Converters = [typeof(Vector3JsonConverter), typeof(QuaternionJsonConverter), typeof(TagJsonConverter)])]
[JsonSerializable(typeof(PropFile))]
[JsonSerializable(typeof(Prop))]
public sealed partial class PropsJsonContext : JsonSerializerContext;
