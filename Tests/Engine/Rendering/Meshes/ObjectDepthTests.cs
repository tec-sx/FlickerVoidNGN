using System.Numerics;

using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Tests.Meshes;

public sealed class ObjectDepthTests
{
    private static readonly Vector3 Ahead = -Vector3.UnitZ;

    [Fact]
    public void TakesSmallObjectsWhole()
    {
        Bounds crate = Box(new Vector3(1, 1, 1));

        float wholeness = ObjectDepth.Wholeness(crate, Ahead);

        wholeness.Should().Be(1);
    }

    [Fact]
    public void TakesObjectsTwiceAsDeepAsTheWholeSizePixelByPixel()
    {
        Bounds ground = Box(new Vector3(1, 1, 2 * ObjectDepth.WholeSize));

        float wholeness = ObjectDepth.Wholeness(ground, Ahead);

        wholeness.Should().Be(0);
    }

    [Fact]
    public void FallsFromWholeToPixelByPixelBetweenTheWholeSizeAndTwiceIt()
    {
        Bounds shed = Box(new Vector3(1, 1, 1.5f * ObjectDepth.WholeSize));

        float wholeness = ObjectDepth.Wholeness(shed, Ahead);

        wholeness.Should().BeApproximately(0.5f, 1e-5f);
    }

    [Fact]
    public void MeasuresHowDeepAnObjectRunsAlongTheView()
    {
        Bounds wall = Box(new Vector3(20, 3, 0.2f));

        float facing = ObjectDepth.Wholeness(wall, Ahead);
        float along = ObjectDepth.Wholeness(wall, Vector3.UnitX);

        facing.Should().Be(1, "a wall facing the view is shallow, however wide");
        along.Should().Be(0, "seen along its length it runs deep");
    }

    [Fact]
    public void MeasuresBoxesSeenAtAnAngle()
    {
        Bounds crate = Box(new Vector3(1.5f, 1.5f, 1.5f));
        Vector3 diagonal = Vector3.Normalize(new Vector3(1, 0, -1));

        float wholeness = ObjectDepth.Wholeness(crate, diagonal);

        float depth = 1.5f * MathF.Sqrt(2);
        float expected = 1 - ((depth - ObjectDepth.WholeSize) / ObjectDepth.WholeSize);
        wholeness.Should().BeApproximately(expected, 1e-5f, "seen corner first, the crate runs from corner to corner");
    }

    private static Bounds Box(Vector3 size)
    {
        return new Bounds(-size / 2, size / 2);
    }
}
