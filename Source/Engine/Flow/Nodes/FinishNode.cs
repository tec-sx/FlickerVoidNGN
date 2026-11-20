using FlickerVoid.Engine.Flow.Graphs;

namespace FlickerVoid.Engine.Flow.Nodes;

/// <summary>Ends the run when a signal reaches it.</summary>
public sealed class FinishNode : FlowNode
{
    public override IReadOnlyList<string> Outputs => [];

    public override void Execute(FlowContext context, string input) => context.FinishRun();
}
