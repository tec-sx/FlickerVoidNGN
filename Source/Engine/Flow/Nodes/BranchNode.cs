using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Flow.Nodes;

/// <summary>Checks a condition set and passes the signal on through True or False.</summary>
public sealed class BranchNode : FlowNode<BranchNode.Out>
{
    public enum Out
    {
        True,
        False,
    }

    public ConditionSet Conditions { get; set; } = new();

    public override string Describe() => Conditions.Describe();

    protected override void Execute(FlowContext<Out> context)
    {
        bool passed = Conditions.Evaluate(context.Conditions);
        Out output = passed ? Out.True : Out.False;
        context.Trigger(output);
    }
}
