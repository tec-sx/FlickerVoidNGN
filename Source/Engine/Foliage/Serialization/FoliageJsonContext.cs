using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Foliage.Serialization;

/// <summary>Source-generated JSON for foliage files and the foliage components: comments and trailing commas allowed, unknown properties rejected.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    UseStringEnumConverter = true,
    Converters = [typeof(Vector3JsonConverter), typeof(QuaternionJsonConverter), typeof(TagJsonConverter)])]
[JsonSerializable(typeof(FoliageFile))]
[JsonSerializable(typeof(Plant))]
[JsonSerializable(typeof(FoliageScatter))]
public sealed partial class FoliageJsonContext : JsonSerializerContext;
