using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Engine.Gameplay.Tests.Facts;

public class FactConditionTests
{
    private readonly IFactDatabase _facts = Substitute.For<IFactDatabase>();
    private readonly ConditionContext _context;
    private readonly FactConditionTestsBuilder _builder = new();

    public FactConditionTests()
    {
        _context = new ConditionContext(_facts);
    }

    [Theory]
    [InlineData(FactCompare.GreaterOrEqual, 4, true)]
    [InlineData(FactCompare.Greater, 4, false)]
    [InlineData(FactCompare.Equal, 4, true)]
    [InlineData(FactCompare.Less, 4, false)]
    public void ComparesFactValue(FactCompare compare, int value, bool expected)
    {
        _facts.GetFact(TestTags.A).Returns(4);
        FactCondition condition = _builder.Comparing(compare, value).Build();

        bool holds = condition.Check(_context);

        holds.Should().Be(expected);
    }

    [Theory]
    [InlineData(FactCompare.Defined, true)]
    [InlineData(FactCompare.Undefined, false)]
    public void ChecksWhetherFactIsDefined(FactCompare compare, bool expected)
    {
        _facts.IsFactDefined(TestTags.A).Returns(true);
        FactCondition condition = _builder.Comparing(compare, 0).Build();

        bool holds = condition.Check(_context);

        holds.Should().Be(expected);
    }

    [Fact]
    public void DoesNotScoreWhenInverted()
    {
        _facts.GetFact(TestTags.A).Returns(50);
        FactCondition condition = _builder.Comparing(FactCompare.GreaterOrEqual, 80).Inverted().Build();

        bool holds = condition.Check(_context);
        bool scored = condition.TryGetScore(_context, out _);

        holds.Should().BeTrue();
        scored.Should().BeFalse();
    }

    [Theory]
    [InlineData(FactCompare.GreaterOrEqual, "Test.FVFramework.A >= 4")]
    [InlineData(FactCompare.Defined, "Test.FVFramework.A defined")]
    public void DescribesComparison(FactCompare compare, string expected)
    {
        FactCondition condition = _builder.Comparing(compare, 4).Build();

        string text = condition.Describe();

        text.Should().Be(expected);
    }
}
