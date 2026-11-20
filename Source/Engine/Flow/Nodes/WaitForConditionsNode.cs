using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Flow.Nodes;

/// <summary>Holds the signal until a condition set passes, checking again whenever a fact changes.</summary>
public sealed class WaitForConditionsNode : FlowNode
{
    public ConditionSet Conditions { get; set; } = new();

    public override string Describe() => Conditions.Describe();

    public override void Execute(FlowContext context, string input)
    {
        if (!TryFinish(context))
        {
            context.StayActive();
        }
    }

    public override void OnFactChanged(FlowContext context) => TryFinish(context);

    private bool TryFinish(FlowContext context)
    {
        if (!Conditions.Evaluate(context.Conditions))
        {
            return false;
        }

        context.Trigger(DefaultOutput);
        return true;
    }
}
