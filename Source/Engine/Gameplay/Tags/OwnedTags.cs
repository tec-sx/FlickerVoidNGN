namespace FlickerVoid.Engine.Gameplay.Tags;

/// <summary>Component listing the tags an entity has, such as those its outfit grants.</summary>
public readonly record struct OwnedTags(IReadOnlyList<Tag> Tags)
{
    /// <summary>Whether a tag the entity has is <paramref name="tag"/> or lies under it.</summary>
    public bool HasMatchingTag(Tag tag)
    {
        foreach (Tag owned in Tags)
        {
            if (owned.MatchesTag(tag))
            {
                return true;
            }
        }

        return false;
    }
}
