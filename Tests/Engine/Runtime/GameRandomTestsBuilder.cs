using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Runtime.Tests;

/// <summary>Builds the game's random source under test from a seed.</summary>
internal sealed class GameRandomTestsBuilder
{
    private ulong _seed;

    public static int[] Roll(Random random, int count)
    {
        int[] rolls = new int[count];
        for (int index = 0; index < count; index++)
        {
            rolls[index] = random.Next(1000);
        }

        return rolls;
    }

    public GameRandomTestsBuilder WithSeed(ulong seed)
    {
        _seed = seed;
        return this;
    }

    public GameRandom Build() => new(_seed);
}
