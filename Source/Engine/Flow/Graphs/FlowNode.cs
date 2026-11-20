using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Flow.Graphs;

/// <summary>
/// One step of a flow graph, like a Flow plugin node: it runs when a signal reaches one of its inputs and passes the signal on
/// through its outputs. Nodes derive from <see cref="FlowNode{TOut}"/> or <see cref="FlowNode{TIn, TOut}"/>, whose enums name
/// their pins. Nodes are data shared by every run of their graph; what a run remembers lives in the <see cref="FlowRun"/>.
/// </summary>
public abstract class FlowNode
{
    /// <summary>Names the node in its graph's links; unique within the graph.</summary>
    public string Id { get; set; } = "";

    /// <summary>Where the graph editor draws the node.</summary>
    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>The input pins' names, as links name them; a node without inputs runs when its run starts.</summary>
    [JsonIgnore]
    public abstract IReadOnlyList<string> Inputs { get; }

    [JsonIgnore]
    public abstract IReadOnlyList<string> Outputs { get; }

    /// <summary>A short line the graph editor shows on the node, such as its conditions.</summary>
    public virtual string Describe() => "";

    /// <summary>Runs the node for a signal at the input with this index, or as its run starts when it has no inputs.</summary>
    internal abstract void Execute(FlowRun run, int input);

    internal abstract void OnFactChanged(FlowRun run);

    internal abstract void Cleanup(FlowRun run);
}
