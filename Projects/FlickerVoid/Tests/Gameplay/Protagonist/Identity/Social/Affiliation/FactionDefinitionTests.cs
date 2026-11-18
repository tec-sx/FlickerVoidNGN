using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Affiliation;

public class FactionDefinitionTests
{
    private static readonly Tag Hated = new("Social.Band.Hated");
    private static readonly Tag Liked = new("Social.Band.Liked");
    private static readonly Tag Revered = new("Social.Band.Revered");

    [Theory]
    [InlineData(-150, Attitude.Neutral)]
    [InlineData(-50, Attitude.Hostile)]
    [InlineData(30, Attitude.Friendly)]
    [InlineData(80, Attitude.Neutral)]
    public void TakesAttitudeFromStandingBand(int standing, Attitude expected)
    {
        ScaleDefinition scale = new()
        {
            Bands =
            [
                new ScaleBand { Threshold = -100, Tag = Hated },
                new ScaleBand { Threshold = 25, Tag = Liked },
                new ScaleBand { Threshold = 75, Tag = Revered },
            ],
        };
        FactionDefinition faction = new()
        {
            Standing = scale,
            BandAttitudes = new() { [Hated] = Attitude.Hostile, [Liked] = Attitude.Friendly },
        };

        Attitude attitude = faction.GetStandingAttitude(standing);

        Assert.Equal(expected, attitude);
    }

    [Fact]
    public void StandingFeedsFameAndRivalStanding()
    {
        Tag fameFact = new("Fact.Social.Fame");
        Tag policeFact = new("Fact.Social.Standing.Police");
        Tag gangFact = new("Fact.Social.Standing.Gang");
        FactSettings settings = new()
        {
            Definitions =
            [
                new FactDefinition { Tag = fameFact, Clamp = true, Min = 0, Max = 100 },
                new FactDefinition { Tag = policeFact, Clamp = true, Min = -100, Max = 100 },
                new FactDefinition { Tag = gangFact, Clamp = true, Min = -100, Max = 100 },
            ],
        };
        FactDatabase facts = new(settings);
        ScaleDefinition fame = new() { Id = fameFact };
        ScaleDefinition gang = new() { Id = gangFact };
        ScaleDefinition police = new()
        {
            Id = policeFact,
            Links = [new ScaleLink { Scale = fame, Factor = 0.5f, Magnitude = true }, new ScaleLink { Scale = gang, Factor = -1f }],
        };

        police.Modify(facts, 20);
        police.Modify(facts, -10);

        Assert.Equal(10, facts.GetFact(policeFact));
        Assert.Equal(15, facts.GetFact(fameFact));
        Assert.Equal(-10, facts.GetFact(gangFact));
    }
}
