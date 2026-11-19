using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering;

/// <summary>Source-generated JSON for rendering components, with the same rules as the runtime's files.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    UseStringEnumConverter = true,
    Converters = [typeof(Vector3JsonConverter)])]
[JsonSerializable(typeof(Camera))]
[JsonSerializable(typeof(MeshRenderer))]
[JsonSerializable(typeof(PointLight))]
[JsonSerializable(typeof(SpotLight))]
[JsonSerializable(typeof(IrradianceVolume))]
[JsonSerializable(typeof(LightingMood))]
public sealed partial class RenderingJsonContext : JsonSerializerContext;
