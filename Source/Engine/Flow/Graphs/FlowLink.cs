namespace FlickerVoid.Engine.Flow.Graphs;

/// <summary>A wire from an output pin of one node to an input pin of another, by node id and pin name.</summary>
public sealed class FlowLink
{
    public string From { get; set; } = "";

    public string Output { get; set; } = FlowNode.DefaultOutput;

    public string To { get; set; } = "";

    public string Input { get; set; } = FlowNode.DefaultInput;
}
