using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Cameras.Layers;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Cameras.Serialization;

/// <summary>Source-generated JSON for the camera components scenes hold and the camera rigs data assets hold.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true,
    Converters = [typeof(Vector3JsonConverter), typeof(QuaternionJsonConverter), typeof(TagJsonConverter)])]
[JsonSerializable(typeof(Shot))]
[JsonSerializable(typeof(CameraTarget))]
[JsonSerializable(typeof(CameraRig))]
[JsonSerializable(typeof(FollowLayer))]
[JsonSerializable(typeof(LookLayer))]
[JsonSerializable(typeof(ShakeLayer))]
[JsonSerializable(typeof(ZoomPunchLayer))]
public sealed partial class CamerasJsonContext : JsonSerializerContext;
