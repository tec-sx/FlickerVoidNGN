using System.Text.Json.Serialization;

using FlickerVoid.Engine.Ui.Stages;

namespace FlickerVoid.Engine.Ui.Serialization;

/// <summary>Source-generated JSON for the interface's scene components.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Turntable))]
public sealed partial class UiJsonContext : JsonSerializerContext;
