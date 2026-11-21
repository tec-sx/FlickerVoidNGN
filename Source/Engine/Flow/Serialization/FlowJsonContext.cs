using System.Text.Json.Serialization;

using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Flow.Nodes;

namespace FlickerVoid.Engine.Flow.Serialization;

/// <summary>Source-generated JSON for flow graphs and the core nodes.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(FlowGraph))]
[JsonSerializable(typeof(StartNode))]
[JsonSerializable(typeof(FinishNode))]
[JsonSerializable(typeof(BranchNode))]
[JsonSerializable(typeof(ApplyEffectsNode))]
[JsonSerializable(typeof(WaitForConditionsNode))]
public sealed partial class FlowJsonContext : JsonSerializerContext;
