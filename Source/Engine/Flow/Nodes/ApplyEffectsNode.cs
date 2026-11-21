using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Flow.Nodes;

/// <summary>Applies an effect list (facts, knowledge, reputation, items...) and passes the signal on.</summary>
public sealed class ApplyEffectsNode : FlowNode<FlowOutput>
{
    public EffectList Effects { get; set; } = new();

    public override string Describe() => Effects.Describe();

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        Effects.Apply(context.Conditions);
        context.Trigger(FlowOutput.Out);
    }
}
