using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Platform.Input.Gestures;

namespace FlickerVoid.Engine.Interaction.Interactables;

/// <summary>One thing an interactable offers: the input that does it, the action it does, the gesture it takes, and the rules and effects of doing it.</summary>
public sealed class InteractionOffer
{
    /// <summary>The input action that does it, such as <c>Input.Use</c>.</summary>
    public Tag Input { get; set; }

    /// <summary>What it does, such as <c>Interaction.Action.Open</c>; responses such as toggles and locks act on it.</summary>
    public Tag Action { get; set; }

    public Gesture Gesture { get; set; } = new();

    public DisplayInfo Display { get; set; } = new();

    /// <summary>What must hold for it to be done, checked with the interactor as instigator and the interactable as target.</summary>
    public ConditionSet Conditions { get; set; } = new();

    /// <summary>Applied when it is done.</summary>
    public EffectList Effects { get; set; } = new();

    /// <summary>Heavier offers are listed first.</summary>
    public int Weight { get; set; }

    /// <summary>How many times it can be done; -1 is unlimited.</summary>
    public int Uses { get; set; } = -1;

    public bool IsValid => Input.IsValid && Action.IsValid;
}
