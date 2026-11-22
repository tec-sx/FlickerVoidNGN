using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Cameras.Serialization;

/// <summary>Source-generated JSON for the camera components scenes hold.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true,
    Converters = [typeof(Vector3JsonConverter), typeof(QuaternionJsonConverter), typeof(TagJsonConverter)])]
[JsonSerializable(typeof(Shot))]
public sealed partial class CamerasJsonContext : JsonSerializerContext;
