using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Abilities.Definitions;

/// <summary>What every ability with a tag also blocks, cancels, requires and is blocked by.</summary>
public sealed class AbilityTagRelationship
{
    public Tag AbilityTag { get; set; }

    public List<Tag> BlockAbilitiesWithTags { get; set; } = [];

    public List<Tag> CancelAbilitiesWithTags { get; set; } = [];

    public List<Tag> RequiredTags { get; set; } = [];

    public List<Tag> BlockedTags { get; set; } = [];
}
