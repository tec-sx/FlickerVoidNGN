using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Creations.Tests;

/// <summary>Reads the test kind's component from scene files.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TestCreation))]
internal sealed partial class CreationsTestsJsonContext : JsonSerializerContext;
