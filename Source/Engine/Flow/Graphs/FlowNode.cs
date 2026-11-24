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

    internal abstract void Resume(FlowRun run);
}

/// <summary>A flow node with the one input <see cref="FlowInput.In"/> and the output pins of <typeparamref name="TOut"/>.</summary>
public abstract class FlowNode<TOut> : FlowNode<FlowInput, TOut>
    where TOut : struct, Enum
{
    protected abstract void Execute(FlowContext<TOut> context);

    protected sealed override void Execute(FlowContext<TOut> context, FlowInput input) => Execute(context);
}

/// <summary>A flow node whose input pins are the values of <typeparamref name="TIn"/> and output pins those of <typeparamref name="TOut"/>.</summary>
public abstract class FlowNode<TIn, TOut> : FlowNode
    where TIn : struct, Enum
    where TOut : struct, Enum
{
    [JsonIgnore]
    public sealed override IReadOnlyList<string> Inputs => FlowPins<TIn>.Names;

    [JsonIgnore]
    public sealed override IReadOnlyList<string> Outputs => FlowPins<TOut>.Names;

    /// <summary>Runs the node for a signal at <paramref name="input"/>; it triggers outputs, stays active or finishes the run.</summary>
    protected abstract void Execute(FlowContext<TOut> context, TIn input);

    /// <summary>Called on an active node after a fact changed, so a waiting node can check again.</summary>
    protected virtual void OnFactChanged(FlowContext<TOut> context)
    {
    }

    /// <summary>Called when an active node stops being active: it finished or its run ended.</summary>
    protected virtual void Cleanup(FlowContext<TOut> context)
    {
    }

    /// <summary>
    /// Called when a run is resumed, such as after a load, with this node active when it was saved: by default it runs again from its
    /// first input, as a wait checks again; a node whose work was already done, such as a camera switch the save holds, passes the signal on.
    /// </summary>
    protected virtual void Resume(FlowContext<TOut> context)
    {
        Execute(context, default);
    }

    internal sealed override void Execute(FlowRun run, int input)
    {
        TIn pin = default;
        if (input >= 0)
        {
            pin = FlowPins<TIn>.Values[input];
        }

        FlowContext<TOut> context = new(run, this);
        Execute(context, pin);
    }

    internal sealed override void OnFactChanged(FlowRun run)
    {
        FlowContext<TOut> context = new(run, this);
        OnFactChanged(context);
    }

    internal sealed override void Cleanup(FlowRun run)
    {
        FlowContext<TOut> context = new(run, this);
        Cleanup(context);
    }

    internal sealed override void Resume(FlowRun run)
    {
        FlowContext<TOut> context = new(run, this);
        Resume(context);
    }
}

/// <summary>The pin of a node with one input.</summary>
public enum FlowInput
{
    In,
}

/// <summary>The pin of a node with one output.</summary>
public enum FlowOutput
{
    Out,
}

/// <summary>No pins: the inputs of a node that runs as its run starts, or the outputs of one that ends it.</summary>
public enum NoPins
{
}

/// <summary>The pins an enum declares, in declared order: their names, as links name them, and their values.</summary>
internal static class FlowPins<T>
    where T : struct, Enum
{
    public static readonly T[] Values = Enum.GetValues<T>();

    public static readonly string[] Names = Enum.GetNames<T>();

    public static int IndexOf(T pin) => Array.IndexOf(Values, pin);
}
