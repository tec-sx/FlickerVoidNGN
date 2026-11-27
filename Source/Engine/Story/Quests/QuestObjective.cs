using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Localization;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Story.Quests;

/// <summary>One step of a quest, done when its fact is set to 1.</summary>
public sealed class QuestObjective
{
    /// <summary>The fact set to 1 when the objective is done.</summary>
    public Tag Fact { get; set; }

    public LocalizedText Description { get; set; } = new();

    /// <summary>Checked whenever a fact changes while the quest is active; empty means only a flow or effect completes it.</summary>
    public ConditionSet CompleteWhen { get; set; } = new();

    /// <summary>The objective is shown and checked only while these pass; empty means always.</summary>
    public ConditionSet VisibleWhen { get; set; } = new();

    /// <summary>Not needed for the quest to complete.</summary>
    public bool Optional { get; set; }
}
