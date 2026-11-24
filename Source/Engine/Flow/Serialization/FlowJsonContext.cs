using System.Text.Json.Serialization;

using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Flow.Nodes;
using FlickerVoid.Engine.Flow.Scenes;

namespace FlickerVoid.Engine.Flow.Serialization;

/// <summary>Source-generated JSON for flow graphs, the core nodes and the travel node and effect.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(FlowGraph))]
[JsonSerializable(typeof(StartNode))]
[JsonSerializable(typeof(FinishNode))]
[JsonSerializable(typeof(BranchNode))]
[JsonSerializable(typeof(ApplyEffectsNode))]
[JsonSerializable(typeof(WaitForConditionsNode))]
[JsonSerializable(typeof(TravelNode))]
[JsonSerializable(typeof(TravelEffect))]
public sealed partial class FlowJsonContext : JsonSerializerContext;
