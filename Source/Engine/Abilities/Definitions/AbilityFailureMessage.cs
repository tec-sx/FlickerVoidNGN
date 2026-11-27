using FlickerVoid.Engine.Gameplay.Localization;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Abilities.Definitions;

/// <summary>What the player is told when the ability fails for a reason.</summary>
public sealed class AbilityFailureMessage
{
    public Tag Reason { get; set; }

    public LocalizedText Text { get; set; } = new();
}
