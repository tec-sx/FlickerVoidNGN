using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social;

public class SocialSettingsTests
{
    [Fact]
    public void KnowsCharactersWhoseRelationshipIsSet()
    {
        ScaleDefinition metRelationship = new() { Id = new Tag("Fact.Relationship.Met") };
        ScaleDefinition strangerRelationship = new() { Id = new Tag("Fact.Relationship.Stranger") };
        CharacterDefinition met = new() { Fragments = [new SocialFragment { Relationship = metRelationship }] };
        CharacterDefinition stranger = new() { Fragments = [new SocialFragment { Relationship = strangerRelationship }] };
        CharacterDefinition unsocial = new();
        SocialSettings settings = new SocialSettingsTestsBuilder()
            .WithRelationship(met)
            .WithRelationship(stranger)
            .WithRelationship(unsocial)
            .Build();
        IFactDatabase facts = Substitute.For<IFactDatabase>();
        facts.IsFactDefined(metRelationship.Id).Returns(true);

        List<CharacterDefinition> known = settings.GetKnownCharacters(facts);

        known.Should().Equal(met);
    }
}
