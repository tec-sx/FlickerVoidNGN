using FlickerVoid.Engine.Flow.Graphs;

namespace FlickerVoid.Engine.Flow.Nodes;

/// <summary>Ends the run when a signal reaches it.</summary>
public sealed class FinishNode : FlowNode<NoPins>
{
    protected override void Execute(FlowContext<NoPins> context) => context.FinishRun();
}
