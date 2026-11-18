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
        FactionDefinition faction = new FactionDefinitionTestsBuilder()
            .WithBand(-100, Hated)
            .WithBand(25, Liked)
            .WithBand(75, Revered)
            .WithAttitude(Hated, Attitude.Hostile)
            .WithAttitude(Liked, Attitude.Friendly)
            .Build();

        Attitude attitude = faction.GetStandingAttitude(standing);

        attitude.Should().Be(expected);
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
        FactionDefinition police = new FactionDefinitionTestsBuilder()
            .WithStandingFact(policeFact)
            .WithLink(fame, 0.5f, magnitude: true)
            .WithLink(gang, -1f)
            .Build();

        police.Standing.Modify(facts, 20);
        police.Standing.Modify(facts, -10);

        IReadOnlyDictionary<Tag, int> values = facts.AllFacts;
        values.Should().Equal(new Dictionary<Tag, int> { [policeFact] = 10, [fameFact] = 15, [gangFact] = -10 });
    }
}
