using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Story.Quests;

/// <summary>Starts, completes or fails a quest and passes the signal on, whether the quest could change or not.</summary>
public sealed class SetQuestStateNode : FlowNode<FlowOutput>
{
    public Tag Quest { get; set; }

    public QuestState State { get; set; } = QuestState.Active;

    public override string Describe() => $"{Quest} = {State}";

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        QuestSystem? quests = context.Conditions.GetService<QuestSystem>();
        quests?.SetState(Quest, State);
        context.Trigger(FlowOutput.Out);
    }
}
