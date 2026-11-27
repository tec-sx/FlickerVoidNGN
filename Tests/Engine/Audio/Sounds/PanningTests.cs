using System.Numerics;

using FlickerVoid.Engine.Audio.Sounds;

namespace FlickerVoid.Engine.Audio.Tests.Sounds;

public sealed class PanningTests
{
    [Fact]
    public void IsEvenInBothEarsStraightAhead()
    {
        (float left, float right) = Panning.Compute(Vector3.Zero, Vector3.UnitX, new Vector3(0, 0, -0.5f), 1, 20);

        left.Should().BeApproximately(right, 0.0001f);
        left.Should().BeApproximately(MathF.Sqrt(0.5f), 0.0001f);
    }

    [Fact]
    public void IsLouderInTheEarOnItsSideButNeverGoneFromTheOther()
    {
        (float left, float right) = Panning.Compute(Vector3.Zero, Vector3.UnitX, new Vector3(0.5f, 0, 0), 1, 20);

        right.Should().BeGreaterThan(left * 2);
        left.Should().BeGreaterThan(0.1f);
    }

    [Fact]
    public void FadesWithDistanceToNothingAtItsFarthest()
    {
        (float near, _) = Panning.Compute(Vector3.Zero, Vector3.UnitX, new Vector3(0, 0, -5), 1, 20);
        (float far, _) = Panning.Compute(Vector3.Zero, Vector3.UnitX, new Vector3(0, 0, -15), 1, 20);
        (float gone, _) = Panning.Compute(Vector3.Zero, Vector3.UnitX, new Vector3(0, 0, -20), 1, 20);

        near.Should().BeGreaterThan(far);
        far.Should().BeGreaterThan(0);
        gone.Should().Be(0);
    }
}
