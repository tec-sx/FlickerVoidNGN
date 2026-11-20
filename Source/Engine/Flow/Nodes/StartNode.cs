using FlickerVoid.Engine.Flow.Graphs;

namespace FlickerVoid.Engine.Flow.Nodes;

/// <summary>Where a run begins: its output fires when the run starts.</summary>
public sealed class StartNode : FlowNode
{
    public override IReadOnlyList<string> Inputs => [];

    public override void Execute(FlowContext context, string input) => context.Trigger(DefaultOutput);
}
