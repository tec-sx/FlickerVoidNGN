using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Story.Quests;

/// <summary>Marks an objective done by setting its fact to 1, and passes the signal on.</summary>
public sealed class CompleteObjectiveNode : FlowNode<FlowOutput>
{
    public Tag Objective { get; set; }

    public override string Describe() => Objective.Name;

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        context.Conditions.Facts.SetFact(Objective, 1);
        context.Trigger(FlowOutput.Out);
    }
}
