using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Debug;

/// <summary>Source-generated JSON for the debug layout.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(DebugLayout))]
internal sealed partial class DebugJsonContext : JsonSerializerContext;
