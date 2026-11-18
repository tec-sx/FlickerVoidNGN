using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Relationships;

/// <summary>A kind of gift a character reacts to.</summary>
public sealed class GiftPreference
{
    /// <summary>Matched against the gift's tags; a parent tag matches every child.</summary>
    public Tag Tag { get; init; }

    /// <summary>Relationship change when a gift matches; negative for gifts the character dislikes.</summary>
    public int RelationshipChange { get; init; } = 1;
}
