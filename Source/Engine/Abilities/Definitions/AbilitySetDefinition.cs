using FlickerVoid.Engine.Gameplay.Definitions;

namespace FlickerVoid.Engine.Abilities.Definitions;

/// <summary>Abilities and modifiers granted together, such as the protagonist's or a resident's.</summary>
public sealed class AbilitySetDefinition : Definition
{
    public List<AbilitySetEntry> Abilities { get; set; } = [];

    /// <summary>Modifier files applied while the set is granted, in place of granted Gameplay Effects.</summary>
    public List<string> Modifiers { get; set; } = [];
}
