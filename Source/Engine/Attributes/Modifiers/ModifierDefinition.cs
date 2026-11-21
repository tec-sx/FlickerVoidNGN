using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Attributes.Modifiers;

/// <summary>
/// A lasting change to attributes, in place of a Gameplay Effect: a perk, a status, an outfit bonus. It holds until it is removed,
/// as the game is player-paced; permanent changes go to the base value through the attribute effects.
/// </summary>
public sealed class ModifierDefinition : Definition
{
    public List<AttributeModifier> Changes { get; set; } = [];

    /// <summary>Tags the entity has while the modifier is on, such as <c>Status.Drunk</c>.</summary>
    public List<Tag> GrantedTags { get; set; } = [];
}
