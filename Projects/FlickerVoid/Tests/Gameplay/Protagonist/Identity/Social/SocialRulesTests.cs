using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social;

public class SocialRulesTests
{
    private readonly SocialRulesTestsBuilder _builder = new();

    [Fact]
    public void FindsTheCharacterAndFactionAnEntityIs()
    {
        CharacterDefinition officer = SocialRulesTestsBuilder.CreateMember(_builder.Police);
        IEntityView entity = SocialRulesTestsBuilder.CreateEntity(officer);
        SocialRules rules = _builder.Build();

        CharacterDefinition? character = rules.GetCharacter(entity);
        FactionDefinition? faction = rules.GetFaction(entity);

        character.Should().BeSameAs(officer);
        faction.Should().BeSameAs(_builder.Police);
    }

    [Fact]
    public void EntitiesWithoutAnIdentityHaveNoFaction()
    {
        IEntityView entity = SocialRulesTestsBuilder.CreateEntity(null);
        SocialRules rules = _builder.Build();

        FactionDefinition? faction = rules.GetFaction(entity);
        FactionDefinition? none = rules.GetFaction(null);

        faction.Should().BeNull();
        none.Should().BeNull();
    }

    [Fact]
    public void ReadsTheRelationshipScaleFromTheSocialFragment()
    {
        Engine.Gameplay.Scales.ScaleDefinition relationship = new() { Id = new Tag("Fact.Relationship.Mara") };
        SocialFragment social = new() { Relationship = relationship };
        CharacterDefinition mara = new() { Fragments = [social] };
        SocialRules rules = _builder.Build();

        Engine.Gameplay.Scales.ScaleDefinition? found = rules.GetRelationshipScale(mara);

        found.Should().BeSameAs(relationship);
    }

    [Theory]
    [InlineData("Disguise.Police", true)]
    [InlineData("Outfit.Coat", false)]
    public void IsDisguisedWhenWearingAnythingUnderTheDisguiseRoot(string tag, bool expected)
    {
        IEntityView player = SocialRulesTestsBuilder.CreateEntity(null, new Tag(tag));
        SocialRules rules = _builder.Build();

        bool disguised = rules.IsDisguised(player);

        disguised.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(49, 0, true)]
    [InlineData(50, 0, false)]
    [InlineData(0, 50, false)]
    public void PassesAsAMemberUntilNotorietyReachesTheDisguiseLimit(int factionNotoriety, int globalNotoriety, bool expected)
    {
        IEntityView player = SocialRulesTestsBuilder.CreateEntity(null, SocialRulesTestsBuilder.PoliceDisguise);
        SocialRules rules = _builder
            .WithFact(_builder.Police.Notoriety!.Id, factionNotoriety)
            .WithFact(_builder.Notoriety.Id, globalNotoriety)
            .Build();

        bool disguisedAs = rules.IsDisguisedAs(player, _builder.Police);

        disguisedAs.Should().Be(expected);
    }

    [Fact]
    public void PassesOnlyAsTheFactionWhoseDisguiseIsWorn()
    {
        IEntityView player = SocialRulesTestsBuilder.CreateEntity(null, new Tag("Disguise.Gang"));
        SocialRules rules = _builder.Build();

        bool disguisedAs = rules.IsDisguisedAs(player, _builder.Police);
        FactionDefinition? disguiseFaction = rules.GetDisguiseFaction(player);

        disguisedAs.Should().BeFalse();
        disguiseFaction.Should().BeNull();
    }

    [Fact]
    public void FindsTheFactionTheDisguiseWorksFor()
    {
        IEntityView player = SocialRulesTestsBuilder.CreateEntity(null, SocialRulesTestsBuilder.PoliceDisguise);
        SocialRules rules = _builder.Build();

        FactionDefinition? disguiseFaction = rules.GetDisguiseFaction(player);

        disguiseFaction.Should().BeSameAs(_builder.Police);
    }

    [Theory]
    [InlineData(false, 62)]
    [InlineData(true, 16)]
    public void ADisguiseHidesMostOfTheFame(bool disguised, int expected)
    {
        Tag[] tags = [];
        if (disguised)
        {
            tags = [SocialRulesTestsBuilder.PoliceDisguise];
        }

        IEntityView player = SocialRulesTestsBuilder.CreateEntity(null, tags);
        SocialRules rules = _builder.WithFact(_builder.Fame.Id, 62).Build();

        int fame = rules.GetRecognizedFame(player);

        fame.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 10)]
    public void ReadsTheHigherNotorietyLessAnyDisguise(bool disguised, int expected)
    {
        Tag[] tags = [];
        if (disguised)
        {
            tags = [new Tag("Disguise.Gang")];
        }

        IEntityView player = SocialRulesTestsBuilder.CreateEntity(null, tags);
        SocialRules rules = _builder
            .WithFact(_builder.Police.Notoriety!.Id, 30)
            .WithFact(_builder.Notoriety.Id, 40)
            .Build();

        int notoriety = rules.GetRecognizedNotoriety(player, _builder.Police);

        notoriety.Should().Be(expected);
    }

    [Fact]
    public void MembersAreAllied()
    {
        CharacterDefinition officer = SocialRulesTestsBuilder.CreateMember(_builder.Police);
        IEntityView subject = SocialRulesTestsBuilder.CreateEntity(officer);
        SocialRules rules = _builder.WithFact(_builder.Notoriety.Id, 90).Build();

        Attitude attitude = rules.GetAttitude(_builder.Police, subject);

        attitude.Should().Be(Attitude.Allied);
    }

    [Fact]
    public void AWorkingDisguiseIsAllied()
    {
        IEntityView player = SocialRulesTestsBuilder.CreateEntity(null, SocialRulesTestsBuilder.PoliceDisguise);
        SocialRules rules = _builder.Build();

        Attitude attitude = rules.GetAttitude(_builder.Police, player);

        attitude.Should().Be(Attitude.Allied);
    }

    [Fact]
    public void HighNotorietyIsHostileWhateverTheStanding()
    {
        IEntityView player = SocialRulesTestsBuilder.CreateEntity(null);
        SocialRules rules = _builder
            .WithFact(_builder.Notoriety.Id, 80)
            .WithFact(_builder.Police.Standing.Id, 60)
            .Build();

        Attitude attitude = rules.GetAttitude(_builder.Police, player);

        attitude.Should().Be(Attitude.Hostile);
    }

    [Theory]
    [InlineData(30, Attitude.Friendly)]
    [InlineData(10, Attitude.Neutral)]
    public void OtherwiseTheStandingBandDecides(int standing, Attitude expected)
    {
        IEntityView player = SocialRulesTestsBuilder.CreateEntity(null);
        SocialRules rules = _builder.WithFact(_builder.Police.Standing.Id, standing).Build();

        Attitude attitude = rules.GetAttitude(_builder.Police, player);

        attitude.Should().Be(expected);
    }
}
