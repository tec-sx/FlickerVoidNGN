using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Builder.Shaders;

namespace FlickerVoid.Engine.Builder.Caching;

/// <summary>Source-generated JSON for the builder's files: comments and trailing commas allowed, unknown properties rejected.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    UseStringEnumConverter = true,
    WriteIndented = true)]
[JsonSerializable(typeof(BuildCacheData))]
[JsonSerializable(typeof(ShaderAssetData))]
public sealed partial class BuilderJsonContext : JsonSerializerContext;
