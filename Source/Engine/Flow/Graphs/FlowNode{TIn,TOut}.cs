using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Flow.Graphs;

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
}
