using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Flow.Nodes;

/// <summary>Checks a condition set and passes the signal on through True or False.</summary>
public sealed class BranchNode : FlowNode
{
    public const string True = "True";

    public const string False = "False";

    private static readonly string[] BranchOutputs = [True, False];

    public ConditionSet Conditions { get; set; } = new();

    public override IReadOnlyList<string> Outputs => BranchOutputs;

    public override string Describe() => Conditions.Describe();

    public override void Execute(FlowContext context, string input)
    {
        bool passed = Conditions.Evaluate(context.Conditions);
        string output = passed ? True : False;
        context.Trigger(output);
    }
}
