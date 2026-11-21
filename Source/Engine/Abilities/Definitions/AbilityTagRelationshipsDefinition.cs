using FlickerVoid.Engine.Gameplay.Definitions;

namespace FlickerVoid.Engine.Abilities.Definitions;

/// <summary>Relationships between ability tags, so rules such as "talking is blocked while hiding" live in one place.</summary>
public sealed class AbilityTagRelationshipsDefinition : Definition
{
    public List<AbilityTagRelationship> Relationships { get; set; } = [];

    /// <summary>The relationships whose tag the ability has.</summary>
    public IEnumerable<AbilityTagRelationship> For(AbilityDefinition ability) => Relationships.Where(relationship => ability.HasTag(relationship.AbilityTag));
}
