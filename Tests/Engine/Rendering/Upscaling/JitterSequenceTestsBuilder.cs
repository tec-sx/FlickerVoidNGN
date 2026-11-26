using System.Numerics;

using FlickerVoid.Engine.Rendering.Upscaling;

namespace FlickerVoid.Engine.Rendering.Tests.Upscaling;

/// <summary>Builds a jitter sequence and takes its offsets in turn.</summary>
internal sealed class JitterSequenceTestsBuilder
{
    private readonly JitterSequence _sequence = new();

    /// <summary>The next offsets of a sequence of that many phases.</summary>
    public Vector2[] Take(int count, int phases)
    {
        Vector2[] offsets = new Vector2[count];
        for (int index = 0; index < count; index++)
        {
            offsets[index] = _sequence.Next(phases);
        }

        return offsets;
    }
}
