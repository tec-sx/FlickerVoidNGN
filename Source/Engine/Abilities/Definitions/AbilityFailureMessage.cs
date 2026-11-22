using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Abilities.Definitions;

/// <summary>What the player is told when the ability fails for a reason.</summary>
public sealed class AbilityFailureMessage
{
    public Tag Reason { get; set; }

    public string Text { get; set; } = string.Empty;
}
