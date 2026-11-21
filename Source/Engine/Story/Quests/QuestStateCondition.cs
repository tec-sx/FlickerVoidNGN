using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Story.Quests;

/// <summary>Whether a quest is in a state, read from its fact.</summary>
public sealed class QuestStateCondition : Condition
{
    public Tag Quest { get; set; }

    public QuestState State { get; set; } = QuestState.Active;

    public override string Describe() => $"{Quest} is {State}";

    protected override bool Evaluate(ConditionContext context) => context.Facts.GetFact(Quest) == (int)State;
}
