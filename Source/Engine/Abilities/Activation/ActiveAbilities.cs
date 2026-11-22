using FlickerVoid.Engine.Abilities.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Abilities.Activation;

/// <summary>Granted abilities while the game plays: the specs, the relationships they follow, the sets' modifiers and the tags running abilities added.</summary>
public struct ActiveAbilities
{
    public List<AbilitySpec> Specs { get; set; }

    public AbilityTagRelationshipsDefinition? Relationships { get; set; }

    /// <summary>Handles of the modifiers the sets applied, taken off when the entity stops.</summary>
    public List<Guid> Modifiers { get; set; }

    /// <summary>The running abilities' owned tags added to the entity's <see cref="OwnedTags"/>.</summary>
    public List<Tag> AppliedTags { get; set; }
}
