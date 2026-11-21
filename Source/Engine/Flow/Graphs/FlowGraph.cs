using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Flow.Graphs;

/// <summary>A flow graph file (<c>.fvflow</c>): nodes, each named by <c>$type</c>, and the links between their pins.</summary>
public sealed class FlowGraph
{
    public const string Extension = ".fvflow";

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    public List<FlowNode> Nodes { get; set; } = [];

    public List<FlowLink> Links { get; set; } = [];

    public FlowNode? FindNode(string id)
    {
        return Nodes.Find(node => node.Id == id);
    }
}
