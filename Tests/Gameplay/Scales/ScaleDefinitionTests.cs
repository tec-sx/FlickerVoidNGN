using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;

namespace FlickerVoid.Engine.Gameplay.Tests.Scales;

public class ScaleDefinitionTests
{
    private readonly FactDatabaseTestsBuilder _facts = new FactDatabaseTestsBuilder()
        .WithRange(TestTags.A, -100, 100)
        .WithRange(TestTags.B, 0, 100);

    private readonly ScaleDefinitionTestsBuilder _builder = new();

    [Fact]
    public void ReturnsChangeThatAppliedAfterClamping()
    {
        FactDatabase facts = _facts.Build();
        ScaleDefinition scale = _builder.WithFact(TestTags.A).Build();

        int within = scale.Modify(facts, 30);
        int clamped = scale.Modify(facts, 100);
        int atEdge = scale.Modify(facts, 10);

        int value = scale.GetValue(facts);
        within.Should().Be(30);
        clamped.Should().Be(70);
        atEdge.Should().Be(0);
        value.Should().Be(100);
    }

    [Theory]
    [InlineData(40, 100, 5)]
    [InlineData(-20, 70, 10)]
    public void PassesOnlyAppliedChangeThroughLinks(int delta, int expectedStanding, int expectedFame)
    {
        FactDatabase facts = _facts.WithFact(TestTags.A, 90).Build();
        ScaleDefinition fame = new ScaleDefinitionTestsBuilder().WithFact(TestTags.B).Build();
        ScaleDefinition standing = _builder.WithFact(TestTags.A).WithLink(fame, 0.5f, magnitude: true).Build();

        standing.Modify(facts, delta);

        int standingValue = standing.GetValue(facts);
        int fameValue = fame.GetValue(facts);
        standingValue.Should().Be(expectedStanding);
        fameValue.Should().Be(expectedFame);
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(-5, -2)]
    public void RoundsPassedChangeHalfUp(int delta, int expected)
    {
        FactDatabase facts = _facts.Build();
        ScaleDefinition linked = new ScaleDefinitionTestsBuilder().WithFact(TestTags.A).Build();
        ScaleDefinition scale = _builder.WithFact(TestTags.GroupOne).WithLink(linked, 0.5f).Build();

        scale.Modify(facts, delta);

        int passed = linked.GetValue(facts);
        passed.Should().Be(expected);
    }

    [Fact]
    public void FindsHighestBandValueReaches()
    {
        ScaleBand low = new() { Threshold = 0 };
        ScaleBand high = new() { Threshold = 50 };
        ScaleDefinition scale = _builder.WithBand(low).WithBand(high).Build();

        ScaleBand? below = scale.FindBand(-1);
        ScaleBand? middle = scale.FindBand(49);
        ScaleBand? top = scale.FindBand(50);

        below.Should().BeNull();
        middle.Should().BeSameAs(low);
        top.Should().BeSameAs(high);
    }

    [Fact]
    public void ReadsRangeFromFactDefinition()
    {
        FactDatabase facts = _facts.Build();
        ScaleDefinition ranged = _builder.WithFact(TestTags.B).Build();
        ScaleDefinition unranged = new ScaleDefinitionTestsBuilder().WithFact(TestTags.GroupOne).Build();

        bool hasRange = ranged.TryGetRange(facts.Settings, out int min, out int max);
        bool hasNoRange = unranged.TryGetRange(facts.Settings, out _, out _);

        hasRange.Should().BeTrue();
        (min, max).Should().Be((0, 100));
        hasNoRange.Should().BeFalse();
    }
}
