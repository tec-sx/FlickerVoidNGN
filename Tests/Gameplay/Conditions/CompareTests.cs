using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Gameplay.Tests.Conditions;

public class CompareTests
{
    [Theory]
    [InlineData(3, Compare.Equal, 3, true)]
    [InlineData(3, Compare.NotEqual, 4, true)]
    [InlineData(4, Compare.Greater, 3, true)]
    [InlineData(3, Compare.Greater, 3, false)]
    [InlineData(3, Compare.GreaterOrEqual, 3, true)]
    [InlineData(2, Compare.Less, 3, true)]
    [InlineData(3, Compare.Less, 3, false)]
    [InlineData(3, Compare.LessOrEqual, 3, true)]
    public void ComparesIntegersWithEveryOperator(int current, Compare compare, int value, bool expected)
    {
        bool result = compare.Test(current, value);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void AllowsForFloatErrorInEqualAndNotEqual()
    {
        float sum = 0.1f + 0.2f;

        bool equal = Compare.Equal.Test(sum, 0.3f);
        bool notEqual = Compare.NotEqual.Test(sum, 0.3f);

        Assert.True(equal);
        Assert.False(notEqual);
    }

    [Fact]
    public void ScoresOnlyAtLeastComparisons()
    {
        bool atLeast = Compare.GreaterOrEqual.TryScore(85f, 80f, out ConditionScore score);
        bool atMost = Compare.LessOrEqual.TryScore(85f, 80f, out _);
        bool equal = Compare.Equal.TryScore(80f, 80f, out _);

        Assert.True(atLeast);
        Assert.Equal(80f, score.Threshold);
        Assert.Equal(5f, score.Excess);
        Assert.False(atMost);
        Assert.False(equal);
    }

    [Fact]
    public void RanksHigherThresholdAboveLargerExcess()
    {
        ConditionScore hard = ConditionScore.AtLeast(80f, 80f);
        ConditionScore easy = ConditionScore.AtLeast(100f, 50f);

        Assert.True(hard > easy);
        Assert.False(easy > hard);
    }

    [Theory]
    [InlineData(Compare.Equal, "==")]
    [InlineData(Compare.GreaterOrEqual, ">=")]
    [InlineData(Compare.LessOrEqual, "<=")]
    public void ShowsOperatorSymbols(Compare compare, string expected)
    {
        string symbol = compare.ToSymbol();

        Assert.Equal(expected, symbol);
    }
}
