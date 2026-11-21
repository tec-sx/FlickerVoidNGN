using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Dialogue.Conversations;

/// <summary>One option of a choice node; its conditions' failure presentation hides it or shows it locked.</summary>
public sealed class DialogueChoiceOption
{
    public string Text { get; set; } = "";

    public ConditionSet Conditions { get; set; } = new();

    public EffectList OnChosen { get; set; } = new();
}
