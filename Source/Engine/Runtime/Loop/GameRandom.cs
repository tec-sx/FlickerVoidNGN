namespace FlickerVoid.Engine.Runtime.Loop;

/// <summary>
/// The game's one source of chance (SplitMix64): seeded, and saved with the game through its <see cref="State"/>, so loading a save
/// rolls what the save would have rolled.
/// </summary>
public sealed class GameRandom : Random
{
    public GameRandom()
        : this((ulong)Shared.NextInt64())
    {
    }

    public GameRandom(ulong seed)
    {
        State = seed;
    }

    /// <summary>Where the sequence is; setting a state back rolls the same numbers again.</summary>
    public ulong State { get; set; }

    public override int Next()
    {
        while (true)
        {
            int value = (int)(NextUInt64() >> 33);
            if (value != int.MaxValue)
            {
                return value;
            }
        }
    }

    public override int Next(int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return (int)NextBelow((ulong)maxValue);
    }

    public override int Next(int minValue, int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
        ulong range = (ulong)((long)maxValue - minValue);
        return (int)(minValue + (long)NextBelow(range));
    }

    public override long NextInt64()
    {
        while (true)
        {
            long value = (long)(NextUInt64() >> 1);
            if (value != long.MaxValue)
            {
                return value;
            }
        }
    }

    public override long NextInt64(long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return (long)NextBelow((ulong)maxValue);
    }

    public override long NextInt64(long minValue, long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
        ulong range = (ulong)(maxValue - minValue);
        return minValue + (long)NextBelow(range);
    }

    public override double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public override float NextSingle() => (NextUInt64() >> 40) * (1.0f / (1U << 24));

    public override void NextBytes(byte[] buffer) => NextBytes(buffer.AsSpan());

    public override void NextBytes(Span<byte> buffer)
    {
        for (int index = 0; index < buffer.Length; index++)
        {
            buffer[index] = (byte)(NextUInt64() >> 56);
        }
    }

    protected override double Sample() => NextDouble();

    private ulong NextUInt64()
    {
        State += 0x9E3779B97F4A7C15;
        ulong value = State;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EB;
        return value ^ (value >> 31);
    }

    /// <summary>A number from zero up to the range, unbiased (Lemire's multiply and reject, as .NET's own generator does).</summary>
    private ulong NextBelow(ulong range)
    {
        ulong next = NextUInt64();
        ulong high = Math.BigMul(next, range, out ulong low);
        if (low < range)
        {
            ulong threshold = (0 - range) % range;
            while (low < threshold)
            {
                next = NextUInt64();
                high = Math.BigMul(next, range, out low);
            }
        }

        return high;
    }
}
