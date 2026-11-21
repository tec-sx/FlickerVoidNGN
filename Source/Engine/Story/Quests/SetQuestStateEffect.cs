using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Story.Quests;

/// <summary>Starts, completes or fails a quest through the <see cref="QuestSystem"/>.</summary>
public sealed class SetQuestStateEffect : Effect
{
    public Tag Quest { get; set; }

    /// <summary>Active, Completed or Failed; NotStarted does nothing.</summary>
    public QuestState State { get; set; } = QuestState.Active;

    public override void Apply(ConditionContext context)
    {
        QuestSystem? quests = context.GetService<QuestSystem>();
        quests?.SetState(Quest, State);
    }

    public override string Describe() => $"Set {Quest} to {State}";
}
