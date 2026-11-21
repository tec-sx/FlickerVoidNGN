using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Abilities.Activation;

/// <summary>The ability sets an entity is granted and the tag relationships its abilities follow; while the game plays they are held by an <see cref="ActiveAbilities"/>.</summary>
public struct GrantedAbilities
{
    public GrantedAbilities()
    {
    }

    [ContentFile(".fvdata")]
    public List<string> Sets { get; set; } = [];

    /// <summary>An <c>abilityTagRelationships</c> file; none when abilities follow only their own tags.</summary>
    [ContentFile(".fvdata")]
    public string? Relationships { get; set; }
}
