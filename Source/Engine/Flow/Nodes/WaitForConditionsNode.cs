using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Flow.Nodes;

/// <summary>Holds the signal until a condition set passes, checking again whenever a fact changes.</summary>
public sealed class WaitForConditionsNode : FlowNode<FlowOutput>
{
    public ConditionSet Conditions { get; set; } = new();

    public override string Describe() => Conditions.Describe();

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        if (!TryFinish(context))
        {
            context.StayActive();
        }
    }

    protected override void OnFactChanged(FlowContext<FlowOutput> context) => TryFinish(context);

    private bool TryFinish(FlowContext<FlowOutput> context)
    {
        if (!Conditions.Evaluate(context.Conditions))
        {
            return false;
        }

        context.Trigger(FlowOutput.Out);
        return true;
    }
}
