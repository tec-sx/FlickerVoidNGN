using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Builder.Caching;

/// <summary>Source-generated JSON for the builder's own files.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(BuildCacheData))]
public sealed partial class BuilderJsonContext : JsonSerializerContext;
