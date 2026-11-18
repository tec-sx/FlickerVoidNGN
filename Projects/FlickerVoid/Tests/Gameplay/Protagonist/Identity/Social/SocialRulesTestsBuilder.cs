using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;
using EntityIdentity = FlickerVoid.Engine.Gameplay.Entities.Identity;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social;

/// <summary>Builds the social rules under test on real facts, with fame, global notoriety and a police faction, and makes entities for them to read.</summary>
internal sealed class SocialRulesTestsBuilder
{
    public static readonly Tag DisguiseRoot = new("Disguise");
    public static readonly Tag PoliceDisguise = new("Disguise.Police");
    public static readonly Tag Liked = new("Social.Band.Liked");

    private readonly List<FactionDefinition> _factions = [];

    public SocialRulesTestsBuilder()
    {
        Fame = new ScaleDefinition { Id = new Tag("Fact.Social.Fame") };
        Notoriety = new ScaleDefinition { Id = new Tag("Fact.Social.Notoriety") };
        ScaleDefinition policeStanding = new()
        {
            Id = new Tag("Fact.Social.Standing.Police"),
            Bands = [new ScaleBand { Threshold = 25, Tag = Liked }],
        };
        ScaleDefinition policeNotoriety = new() { Id = new Tag("Fact.Social.Notoriety.Police") };
        Police = new FactionDefinition
        {
            Standing = policeStanding,
            Notoriety = policeNotoriety,
            BandAttitudes = new() { [Liked] = Attitude.Friendly },
            DisguiseTag = PoliceDisguise,
            DisguiseNotorietyLimit = 50,
            HostileNotoriety = 75,
        };
        _factions.Add(Police);

        FactSettings settings = new()
        {
            Definitions =
            [
                new FactDefinition { Tag = Fame.Id, Clamp = true, Min = 0, Max = 100 },
                new FactDefinition { Tag = Notoriety.Id, Clamp = true, Min = 0, Max = 100 },
            ],
        };
        Facts = new FactDatabase(settings);
    }

    public FactDatabase Facts { get; }

    public ScaleDefinition Fame { get; }

    public ScaleDefinition Notoriety { get; }

    public FactionDefinition Police { get; }

    public SocialRulesTestsBuilder WithFaction(FactionDefinition faction)
    {
        _factions.Add(faction);
        return this;
    }

    public SocialRulesTestsBuilder WithFact(Tag fact, int value)
    {
        Facts.SetFact(fact, value);
        return this;
    }

    public SocialRules Build()
    {
        SocialSettings settings = new()
        {
            Fame = Fame,
            Notoriety = Notoriety,
            Factions = [.. _factions],
            DisguiseRoot = DisguiseRoot,
        };
        return new SocialRules(settings, Facts);
    }

    /// <summary>A substitute entity that is the character, when given, and has the tags.</summary>
    public static IEntityView CreateEntity(CharacterDefinition? character, params Tag[] tags)
    {
        IEntityView entity = Substitute.For<IEntityView>();
        if (character is not null)
        {
            EntityIdentity identity = new(character);
            entity.TryGet(out Arg.Any<EntityIdentity>()).Returns(call =>
            {
                call[0] = identity;
                return true;
            });
        }

        OwnedTags owned = new(tags);
        entity.TryGet(out Arg.Any<OwnedTags>()).Returns(call =>
        {
            call[0] = owned;
            return true;
        });
        return entity;
    }

    /// <summary>A character in the faction.</summary>
    public static CharacterDefinition CreateMember(FactionDefinition faction)
    {
        SocialFragment social = new() { Faction = faction };
        return new CharacterDefinition { Fragments = [social] };
    }
}
