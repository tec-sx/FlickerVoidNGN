using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Engine.Gameplay.Tests.Conditions;

public class ConditionSetTests
{
    private readonly IFactDatabase _facts = Substitute.For<IFactDatabase>();
    private readonly ConditionContext _context;

    public ConditionSetTests()
    {
        _context = new ConditionContext(_facts);
    }

    [Fact]
    public void PassesWhenEmptyInEitherMode()
    {
        ConditionSet all = new ConditionSetTestsBuilder().Build();
        ConditionSet any = new ConditionSetTestsBuilder().InAnyMode().Build();

        bool allHolds = all.Evaluate(_context);
        bool anyHolds = any.Evaluate(_context);

        allHolds.Should().BeTrue();
        anyHolds.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    public void NeedsEveryConditionInAllMode(bool first, bool second, bool expected)
    {
        ConditionSet set = new ConditionSetTestsBuilder().WithConstant(first).WithConstant(second).Build();

        bool holds = set.Evaluate(_context);

        holds.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    public void NeedsOneConditionInAnyMode(bool first, bool second, bool expected)
    {
        ConditionSet set = new ConditionSetTestsBuilder().InAnyMode().WithConstant(first).WithConstant(second).Build();

        bool holds = set.Evaluate(_context);

        holds.Should().Be(expected);
    }

    [Fact]
    public void FlipsNestedGroupWithInvert()
    {
        ConditionSet inner = new ConditionSetTestsBuilder().WithConstant(false).Build();
        GroupCondition group = new() { Invert = true, Set = inner };
        ConditionSet set = new ConditionSetTestsBuilder().With(group).Build();

        bool holds = set.Evaluate(_context);

        holds.Should().BeTrue();
    }

    [Fact]
    public void RanksSetsThatHoldHardestFirst()
    {
        _facts.GetFact(TestTags.A).Returns(50);
        ConditionSet[] sets =
        [
            new ConditionSetTestsBuilder().WithAtLeast(TestTags.A, 20).Build(),
            new ConditionSetTestsBuilder().WithAtLeast(TestTags.A, 80).Build(),
            new ConditionSetTestsBuilder().WithAtLeast(TestTags.A, 40).Build(),
            new ConditionSetTestsBuilder().WithConstant(true).Build(),
        ];

        int[] ranked = ConditionSet.Rank(sets, _context);

        ranked.Should().Equal(2, 0, 3);
    }

    [Fact]
    public void ScoresAllSetsAsSumAndAnySetsAsBest()
    {
        _facts.GetFact(TestTags.A).Returns(50);
        _facts.GetFact(TestTags.B).Returns(30);
        ConditionSet all = new ConditionSetTestsBuilder().WithAtLeast(TestTags.A, 40).WithAtLeast(TestTags.B, 20).Build();
        ConditionSet any = new ConditionSetTestsBuilder().InAnyMode().WithAtLeast(TestTags.A, 40).WithAtLeast(TestTags.B, 20).Build();

        bool allScored = all.TryGetScore(_context, out ConditionScore allScore);
        bool anyScored = any.TryGetScore(_context, out ConditionScore anyScore);

        allScored.Should().BeTrue();
        allScore.Should().Be(new ConditionScore(60f, 20f));
        anyScored.Should().BeTrue();
        anyScore.Threshold.Should().Be(40f);
    }

    [Fact]
    public void DescribesWithAndOrAndNot()
    {
        FactCondition notB = new() { Fact = TestTags.B, Compare = FactCompare.Defined, Invert = true };
        ConditionSet all = new ConditionSetTestsBuilder().WithAtLeast(TestTags.A, 4).With(notB).Build();
        ConditionSet any = new ConditionSetTestsBuilder().InAnyMode().WithAtLeast(TestTags.A, 4).With(notB).Build();

        string allText = all.Describe();
        string anyText = any.Describe();

        allText.Should().Be("Test.FVFramework.A >= 4 AND NOT Test.FVFramework.B defined");
        anyText.Should().Be("Test.FVFramework.A >= 4 OR NOT Test.FVFramework.B defined");
    }
}
