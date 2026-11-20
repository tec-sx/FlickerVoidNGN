namespace FlickerVoid.Engine.Flow.Graphs;

/// <summary>A flow node with the one input <see cref="FlowInput.In"/> and the output pins of <typeparamref name="TOut"/>.</summary>
public abstract class FlowNode<TOut> : FlowNode<FlowInput, TOut>
    where TOut : struct, Enum
{
    protected abstract void Execute(FlowContext<TOut> context);

    protected sealed override void Execute(FlowContext<TOut> context, FlowInput input) => Execute(context);
}
