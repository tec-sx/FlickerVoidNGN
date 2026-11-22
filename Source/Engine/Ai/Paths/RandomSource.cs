using DotRecast.Core;

namespace FlickerVoid.Engine.Ai.Paths;

/// <summary>Hands Detour the numbers of a <see cref="Random"/>, so a seeded one gives the same random points.</summary>
internal sealed class RandomSource(Random random) : IRcRand
{
    public float Next() => random.NextSingle();

    public double NextDouble() => random.NextDouble();

    public int NextInt32() => random.Next();
}
