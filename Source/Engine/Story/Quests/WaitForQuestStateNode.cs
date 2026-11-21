using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Story.Quests;

/// <summary>Holds the signal until a quest reaches a state, checking again whenever a fact changes.</summary>
public sealed class WaitForQuestStateNode : FlowNode<FlowOutput>
{
    public Tag Quest { get; set; }

    public QuestState State { get; set; } = QuestState.Completed;

    public override string Describe() => $"{Quest} = {State}";

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
        if (context.Conditions.Facts.GetFact(Quest) != (int)State)
        {
            return false;
        }

        context.Trigger(FlowOutput.Out);
        return true;
    }
}
