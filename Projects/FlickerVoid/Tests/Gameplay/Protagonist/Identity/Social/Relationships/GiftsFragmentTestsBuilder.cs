using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Relationships;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Relationships;

/// <summary>Builds the gift preferences under test.</summary>
internal sealed class GiftsFragmentTestsBuilder
{
    private readonly List<GiftPreference> _preferences = [];
    private int _defaultChange;

    public GiftsFragmentTestsBuilder WithPreference(string tag, int relationshipChange)
    {
        GiftPreference preference = new() { Tag = new Tag(tag), RelationshipChange = relationshipChange };
        _preferences.Add(preference);
        return this;
    }

    public GiftsFragmentTestsBuilder WithDefaultChange(int relationshipChange)
    {
        _defaultChange = relationshipChange;
        return this;
    }

    public GiftsFragment Build() => new() { Preferences = [.. _preferences], DefaultRelationshipChange = _defaultChange };
}
