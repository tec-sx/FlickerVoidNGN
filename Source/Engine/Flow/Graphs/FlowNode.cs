using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Flow.Graphs;

/// <summary>
/// One step of a flow graph, like a Flow plugin node: it runs when a signal reaches one of its inputs and passes the signal on
/// through its outputs. Nodes are data shared by every run of their graph; what a run remembers lives in the <see cref="FlowRun"/>.
/// </summary>
public abstract class FlowNode
{
    public const string DefaultInput = "In";

    public const string DefaultOutput = "Out";

    private static readonly string[] DefaultPins = [DefaultInput];
    private static readonly string[] DefaultOutputs = [DefaultOutput];

    /// <summary>Names the node in its graph's links; unique within the graph.</summary>
    public string Id { get; set; } = "";

    /// <summary>Where the graph editor draws the node.</summary>
    public float X { get; set; }

    public float Y { get; set; }

    [JsonIgnore]
    public virtual IReadOnlyList<string> Inputs => DefaultPins;

    [JsonIgnore]
    public virtual IReadOnlyList<string> Outputs => DefaultOutputs;

    /// <summary>A short line the graph editor shows on the node, such as its conditions.</summary>
    public virtual string Describe() => "";

    /// <summary>Runs the node for a signal arriving at <paramref name="input"/>; it triggers outputs, stays active or finishes the run.</summary>
    public abstract void Execute(FlowContext context, string input);

    /// <summary>Called on an active node after a fact changed, so a waiting node can check again.</summary>
    public virtual void OnFactChanged(FlowContext context)
    {
    }

    /// <summary>Called when an active node stops being active: it finished or its run ended.</summary>
    public virtual void Cleanup(FlowContext context)
    {
    }
}
