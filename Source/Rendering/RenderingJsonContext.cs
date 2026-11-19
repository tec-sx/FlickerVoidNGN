using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Rendering;

/// <summary>Source-generated JSON for rendering components, with the same rules as the runtime's files.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Camera))]
[JsonSerializable(typeof(MeshRenderer))]
public sealed partial class RenderingJsonContext : JsonSerializerContext;
