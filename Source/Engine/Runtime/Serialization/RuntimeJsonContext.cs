using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Curves;
using FlickerVoid.Engine.Runtime.Greyboxes;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Runtime.Serialization;

/// <summary>Source-generated JSON for the runtime's files: comments and trailing commas allowed, unknown properties rejected.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    Converters = [typeof(Vector3JsonConverter), typeof(QuaternionJsonConverter)])]
[JsonSerializable(typeof(SceneData))]
[JsonSerializable(typeof(EntityTemplateFile))]
[JsonSerializable(typeof(Transform))]
[JsonSerializable(typeof(Saveable))]
[JsonSerializable(typeof(Greybox))]
[JsonSerializable(typeof(Traveller))]
[JsonSerializable(typeof(SolidMesh))]
[JsonSerializable(typeof(Curve))]
[JsonSerializable(typeof(ColorCurve))]
[JsonSerializable(typeof(JsonObject))]
public sealed partial class RuntimeJsonContext : JsonSerializerContext;
