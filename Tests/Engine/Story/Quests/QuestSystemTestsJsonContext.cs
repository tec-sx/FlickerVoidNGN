using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Story.Tests.Quests;

/// <summary>Source-generated JSON for the tests' own effect.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, Converters = [typeof(TagJsonConverter)])]
[JsonSerializable(typeof(MarkEffect))]
internal sealed partial class QuestSystemTestsJsonContext : JsonSerializerContext;
