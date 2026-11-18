using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;

namespace FlickerVoid.Engine.Gameplay.Tests.Scales;

public class ScaleTests
{
    private readonly FactDatabase _facts;
    private readonly ConditionContext _context;

    public ScaleTests()
    {
        FactSettings settings = new()
        {
            Definitions =
            [
                new FactDefinition { Tag = TestTags.A, Clamp = true, Min = -100, Max = 100 },
                new FactDefinition { Tag = TestTags.B, Clamp = true, Min = 0, Max = 100 },
            ],
        };
        _facts = new FactDatabase(settings);
        _context = new ConditionContext(_facts);
    }

    [Fact]
    public void ReturnsChangeThatAppliedAfterClamping()
    {
        ScaleDefinition scale = new() { Id = TestTags.A };

        int within = scale.Modify(_facts, 30);
        int clamped = scale.Modify(_facts, 100);
        int atEdge = scale.Modify(_facts, 10);

        int value = scale.GetValue(_facts);

        Assert.Equal(30, within);
        Assert.Equal(70, clamped);
        Assert.Equal(0, atEdge);
        Assert.Equal(100, value);
    }

    [Fact]
    public void PassesOnlyAppliedChangeThroughLinks()
    {
        ScaleDefinition fame = new() { Id = TestTags.B };
        ScaleDefinition standing = new() { Id = TestTags.A, Links = [new ScaleLink { Scale = fame, Factor = 0.5f, Magnitude = true }] };
        _facts.SetFact(TestTags.A, 90);

        standing.Modify(_facts, 40);
        Assert.Equal(100, _facts.GetFact(TestTags.A));
        Assert.Equal(5, _facts.GetFact(TestTags.B));

        standing.Modify(_facts, -20);
        Assert.Equal(15, _facts.GetFact(TestTags.B));
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(-5, -2)]
    public void RoundsPassedChangeHalfUp(int delta, int expected)
    {
        ScaleDefinition linked = new() { Id = TestTags.A };
        ScaleDefinition scale = new() { Id = TestTags.GroupOne, Links = [new ScaleLink { Scale = linked, Factor = 0.5f }] };

        scale.Modify(_facts, delta);

        Assert.Equal(expected, _facts.GetFact(TestTags.A));
    }

    [Fact]
    public void FindsHighestBandValueReaches()
    {
        ScaleBand low = new() { Threshold = 0 };
        ScaleBand high = new() { Threshold = 50 };
        ScaleDefinition scale = new() { Bands = [low, high] };

        ScaleBand? below = scale.FindBand(-1);
        ScaleBand? middle = scale.FindBand(49);
        ScaleBand? top = scale.FindBand(50);

        Assert.Null(below);
        Assert.Same(low, middle);
        Assert.Same(high, top);
    }

    [Fact]
    public void ReadsRangeFromFactDefinition()
    {
        ScaleDefinition ranged = new() { Id = TestTags.B };
        ScaleDefinition unranged = new() { Id = TestTags.GroupOne };

        bool hasRange = ranged.TryGetRange(_facts.Settings, out int min, out int max);
        bool hasNoRange = unranged.TryGetRange(_facts.Settings, out _, out _);

        Assert.True(hasRange);
        Assert.Equal((0, 100), (min, max));
        Assert.False(hasNoRange);
    }

    [Fact]
    public void ScoresAndModifiesThroughConditionsAndEffects()
    {
        ScaleDefinition fame = new() { Id = TestTags.B, Display = new() { Name = "Fame" } };
        ScaleCondition famous = new() { Scale = fame, Value = 80 };
        ModifyScaleEffect reward = new() { Scale = fame, Delta = 85 };

        bool before = famous.Check(_context);
        reward.Apply(_context);
        bool after = famous.Check(_context);
        bool scored = famous.TryGetScore(_context, out ConditionScore score);

        Assert.False(before);
        Assert.True(after);
        Assert.True(scored);
        Assert.Equal(new ConditionScore(80f, 5f), score);
        string famousText = famous.Describe();
        string rewardText = reward.Describe();

        Assert.Equal("Fame >= 80", famousText);
        Assert.Equal("Fame 85", rewardText);
    }
}
