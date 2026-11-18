using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Relationships;

/// <summary>How a character takes gifts: matching preferences add up, a gift matching none gets the default change.</summary>
public sealed class GiftsFragment : CharacterFragment
{
    /// <summary>Gifts this character likes or dislikes; at most three.</summary>
    public List<GiftPreference> Preferences { get; init; } = [];

    /// <summary>Relationship change for a gift no preference matches.</summary>
    public int DefaultRelationshipChange { get; init; }

    /// <summary>Relationship change a gift with these tags (its definition's tags and category) makes: the sum of the matching preferences, else the default.</summary>
    public int GetRelationshipChange(IReadOnlyCollection<Tag> giftTags)
    {
        int change = 0;
        bool matched = false;
        foreach (GiftPreference preference in Preferences)
        {
            if (preference.Tag.IsValid && Matches(giftTags, preference.Tag))
            {
                change += preference.RelationshipChange;
                matched = true;
            }
        }

        return matched ? change : DefaultRelationshipChange;
    }

    private static bool Matches(IReadOnlyCollection<Tag> giftTags, Tag preference)
    {
        foreach (Tag tag in giftTags)
        {
            if (tag.MatchesTag(preference))
            {
                return true;
            }
        }

        return false;
    }
}
