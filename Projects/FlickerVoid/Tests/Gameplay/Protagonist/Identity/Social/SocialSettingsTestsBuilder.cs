using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Gameplay.Protagonist.Identity.Social;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social;

/// <summary>Builds the social settings under test.</summary>
internal sealed class SocialSettingsTestsBuilder
{
    private readonly List<CharacterDefinition> _relationships = [];

    public SocialSettingsTestsBuilder WithRelationship(CharacterDefinition character)
    {
        _relationships.Add(character);
        return this;
    }

    public SocialSettings Build() => new() { Relationships = [.. _relationships] };
}
