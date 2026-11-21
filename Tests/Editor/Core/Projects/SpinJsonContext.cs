using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlickerVoid.Editor.Core.Tests.Projects;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Spin))]
internal sealed partial class SpinJsonContext : JsonSerializerContext;
