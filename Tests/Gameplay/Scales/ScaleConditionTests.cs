using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;

namespace FlickerVoid.Engine.Gameplay.Tests.Scales;

public class ScaleConditionTests
{
    private readonly IFactDatabase _facts = Substitute.For<IFactDatabase>();
    private readonly ConditionContext _context;
    private readonly ScaleConditionTestsBuilder _builder;

    public ScaleConditionTests()
    {
        _context = new ConditionContext(_facts);
        ScaleDefinition fame = new ScaleDefinitionTestsBuilder().WithFact(TestTags.B).WithName("Fame").Build();
        _builder = new ScaleConditionTestsBuilder(fame);
    }

    [Theory]
    [InlineData(79, false)]
    [InlineData(80, true)]
    public void ComparesScaleValue(int fame, bool expected)
    {
        _facts.GetFact(TestTags.B).Returns(fame);
        ScaleCondition condition = _builder.Comparing(Compare.GreaterOrEqual, 80).Build();

        bool holds = condition.Check(_context);

        holds.Should().Be(expected);
    }

    [Fact]
    public void ScoresAtLeastComparison()
    {
        _facts.GetFact(TestTags.B).Returns(85);
        ScaleCondition condition = _builder.Comparing(Compare.GreaterOrEqual, 80).Build();

        bool scored = condition.TryGetScore(_context, out ConditionScore score);

        scored.Should().BeTrue();
        score.Should().Be(new ConditionScore(80f, 5f));
    }

    [Fact]
    public void DescribesWithScaleName()
    {
        ScaleCondition condition = _builder.Comparing(Compare.GreaterOrEqual, 80).Build();

        string text = condition.Describe();

        text.Should().Be("Fame >= 80");
    }
}
