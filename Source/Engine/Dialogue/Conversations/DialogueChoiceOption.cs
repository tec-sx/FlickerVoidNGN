using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Localization;

namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>One option of a choice node; its conditions' failure presentation hides it or shows it locked.</summary>
public sealed class DialogueChoiceOption
{
    public LocalizedText Text { get; set; } = new();

    public ConditionSet Conditions { get; set; } = new();

    public EffectList OnChosen { get; set; } = new();
}
