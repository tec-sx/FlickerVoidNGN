using FlickerVoid.Engine.Flow.Graphs;

namespace FlickerVoid.Engine.Flow.Nodes;

/// <summary>Where a run begins: it has no inputs, so it runs as the run starts and fires its output.</summary>
public sealed class StartNode : FlowNode<NoPins, FlowOutput>
{
    protected override void Execute(FlowContext<FlowOutput> context, NoPins input) => context.Trigger(FlowOutput.Out);
}
