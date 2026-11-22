using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Cameras.Layers;
using FlickerVoid.Engine.Cameras.Scripting;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Cameras.Triggers;
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
[JsonSerializable(typeof(TriggerArea))]
[JsonSerializable(typeof(TriggerActivator))]
[JsonSerializable(typeof(CameraCue))]
[JsonSerializable(typeof(SavedCue))]
[JsonSerializable(typeof(SavedCameras))]
[JsonSerializable(typeof(CutToShotEffect))]
[JsonSerializable(typeof(PlayShotEffect))]
[JsonSerializable(typeof(ShakeCameraEffect))]
[JsonSerializable(typeof(SwitchShotNode))]
[JsonSerializable(typeof(PlayShotNode))]
[JsonSerializable(typeof(WaitForShotMarkerNode))]
[JsonSerializable(typeof(FadeNode))]
[JsonSerializable(typeof(ShakeCameraNode))]
public sealed partial class CamerasJsonContext : JsonSerializerContext;
