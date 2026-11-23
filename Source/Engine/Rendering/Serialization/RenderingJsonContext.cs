using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.Serialization;

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
[JsonSerializable(typeof(MainCamera))]
[JsonSerializable(typeof(DepthFadeOverride))]
[JsonSerializable(typeof(MeshRenderer))]
[JsonSerializable(typeof(MeshAnimator))]
[JsonSerializable(typeof(PointLight))]
[JsonSerializable(typeof(SpotLight))]
[JsonSerializable(typeof(IrradianceVolume))]
[JsonSerializable(typeof(LightingMood))]
[JsonSerializable(typeof(TextureSettings))]
[JsonSerializable(typeof(SceneMood))]
[JsonSerializable(typeof(IndoorVolume))]
[JsonSerializable(typeof(Uncut))]
[JsonSerializable(typeof(Doorway))]
[JsonSerializable(typeof(Outline))]
[JsonSerializable(typeof(ShaderParameters))]
[JsonSerializable(typeof(MaterialFile))]
[JsonSerializable(typeof(CanvasFile))]
public sealed partial class RenderingJsonContext : JsonSerializerContext;

