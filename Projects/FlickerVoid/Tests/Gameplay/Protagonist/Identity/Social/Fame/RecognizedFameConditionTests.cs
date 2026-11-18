using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Fame;

public class RecognizedFameConditionTests
{
    private readonly RecognizedFameConditionTestsBuilder _builder = new();

    [Theory]
    [InlineData(49, false)]
    [InlineData(50, true)]
    public void ComparesTheFameOthersRecognizeOnTheInstigator(int fame, bool expected)
    {
        ConditionContext context = _builder.Context;
        _builder.Rules.GetRecognizedFame(context.Instigator).Returns(fame);
        RecognizedFameCondition condition = _builder.WithCompare(Compare.GreaterOrEqual, 50).Build();

        bool holds = condition.Check(context);

        holds.Should().Be(expected);
    }

    [Fact]
    public void ScoresAtLeastComparisons()
    {
        ConditionContext context = _builder.Context;
        _builder.Rules.GetRecognizedFame(context.Instigator).Returns(62);
        RecognizedFameCondition condition = _builder.WithCompare(Compare.GreaterOrEqual, 50).Build();

        bool scored = condition.TryGetScore(context, out ConditionScore score);

        scored.Should().BeTrue();
        score.Should().Be(new ConditionScore(50, 12));
    }

    [Fact]
    public void FailsWithoutSocialRules()
    {
        IFactDatabase facts = Substitute.For<IFactDatabase>();
        ConditionContext context = new(facts);
        RecognizedFameCondition condition = _builder.WithCompare(Compare.LessOrEqual, 0).Build();

        bool holds = condition.Check(context);
        bool scored = condition.TryGetScore(context, out _);

        holds.Should().BeFalse();
        scored.Should().BeFalse();
    }

    [Fact]
    public void DescribesTheComparison()
    {
        RecognizedFameCondition condition = _builder.WithCompare(Compare.GreaterOrEqual, 50).Build();

        string text = condition.Describe();

        text.Should().Be("Recognized fame >= 50");
    }
}
