using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Story.Quests;

/// <summary>A quest; its id is the fact that holds its <see cref="QuestState"/>.</summary>
public sealed class QuestDefinition : Definition
{
    /// <summary>The quest starts by itself when it has not started and these pass; empty means it is only started by a rule.</summary>
    public ConditionSet AutoStartWhen { get; set; } = new();

    public List<QuestObjective> Objectives { get; set; } = [];

    /// <summary>The active quest fails when these pass; empty means never.</summary>
    public ConditionSet FailWhen { get; set; } = new();

    public EffectList OnStarted { get; set; } = new();

    public EffectList OnCompleted { get; set; } = new();

    public EffectList OnFailed { get; set; } = new();

    /// <summary>A flow graph that runs while the quest is active; it starts again from its start after a load.</summary>
    [ContentFile(".fvflow")]
    public string? Flow { get; set; }
}
