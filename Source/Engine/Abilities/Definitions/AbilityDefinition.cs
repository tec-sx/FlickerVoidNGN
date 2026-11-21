using System.Text.Json.Serialization;

using FlickerVoid.Engine.Abilities.Costs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Abilities.Definitions;

/// <summary>
/// Something an entity can do, in place of a Gameplay Ability: when it activates, what blocks, cancels and requires it by tags, its
/// costs and its effects. Its behaviour is data, or a system that handles <c>AbilityActivatedEvent</c> and ends it.
/// </summary>
public sealed class AbilityDefinition : Definition
{
    public AbilityPolicy Policy { get; set; }

    public AbilityGroup Group { get; set; }

    /// <summary>Ends as soon as it has activated and applied its effects.</summary>
    public bool Instant { get; set; }

    /// <summary>Tags the owner must have to activate it.</summary>
    public List<Tag> RequiredTags { get; set; } = [];

    /// <summary>Tags that stop it activating when the owner has any.</summary>
    public List<Tag> BlockedTags { get; set; } = [];

    /// <summary>While it runs, abilities with these tags can't activate.</summary>
    public List<Tag> BlockAbilitiesWithTags { get; set; } = [];

    /// <summary>When it activates, running abilities with these tags are cancelled.</summary>
    public List<Tag> CancelAbilitiesWithTags { get; set; } = [];

    /// <summary>Tags the owner has while it runs, such as <c>Status.Interacting</c>.</summary>
    public List<Tag> OwnedTagsWhileActive { get; set; } = [];

    /// <summary>Checked with the owner as instigator and the ability's target as target.</summary>
    public ConditionSet Conditions { get; set; } = new();

    public List<AbilityCost> Costs { get; set; } = [];

    /// <summary>Applied when it activates, with the owner as instigator and the ability's target as target.</summary>
    public EffectList OnActivated { get; set; } = new();

    public List<AbilityFailureMessage> FailureMessages { get; set; } = [];

    /// <summary>Its id and tags, which other abilities block and cancel it by.</summary>
    [JsonIgnore]
    public IEnumerable<Tag> AllTags => Tags.Prepend(Id);

    /// <summary>Whether its id or one of its tags is <paramref name="tag"/> or lies under it.</summary>
    public bool HasTag(Tag tag) => AllTags.Any(own => own.MatchesTag(tag));

    public bool HasAnyTag(IEnumerable<Tag> tags) => tags.Any(HasTag);
}
