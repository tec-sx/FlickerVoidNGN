using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Runtime.Tests;

public class GameRandomTests
{
    private readonly GameRandomTestsBuilder _builder = new();

    [Fact]
    public void TheSameSeedRollsTheSameNumbers()
    {
        GameRandom first = _builder.WithSeed(42).Build();
        GameRandom second = _builder.WithSeed(42).Build();

        int[] firstRolls = GameRandomTestsBuilder.Roll(first, 8);
        int[] secondRolls = GameRandomTestsBuilder.Roll(second, 8);

        firstRolls.Should().Equal(secondRolls);
        firstRolls.Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void SettingItsStateBackRollsTheSameNumbersAgain()
    {
        GameRandom random = _builder.WithSeed(7).Build();
        GameRandomTestsBuilder.Roll(random, 3);
        ulong state = random.State;
        int[] rolls = GameRandomTestsBuilder.Roll(random, 5);

        random.State = state;

        GameRandomTestsBuilder.Roll(random, 5).Should().Equal(rolls);
    }

    [Fact]
    public void RollsEveryNumberOfARangeAndNothingOutsideIt()
    {
        GameRandom random = _builder.WithSeed(1).Build();
        HashSet<int> faces = [];
        double lowest = 1;
        double highest = 0;

        for (int roll = 0; roll < 600; roll++)
        {
            faces.Add(random.Next(1, 7));
            double fraction = random.NextDouble();
            lowest = Math.Min(lowest, fraction);
            highest = Math.Max(highest, fraction);
        }

        faces.Should().BeEquivalentTo([1, 2, 3, 4, 5, 6]);
        lowest.Should().BeGreaterThanOrEqualTo(0);
        highest.Should().BeLessThan(1);
    }

    [Fact]
    public void AnEmptyRangeRollsItsStart()
    {
        GameRandom random = _builder.WithSeed(3).Build();

        int roll = random.Next(4, 4);

        roll.Should().Be(4);
    }
}
