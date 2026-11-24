using System.Numerics;

using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Runtime.Tests.Geometry;

public sealed class BoundsTests
{
    private const float Precision = 1e-4f;

    private static readonly Bounds UnitCube = new(-Vector3.One, Vector3.One);

    [Fact]
    public void GrowsToHoldWhatItIncludes()
    {
        Bounds bounds = Bounds.Empty.Include(new Vector3(1, 2, 3)).Include(new Vector3(-1, 0, 5));

        bounds.Should().Be(new Bounds(new Vector3(-1, 0, 3), new Vector3(1, 2, 5)));
        Bounds.Empty.IsEmpty.Should().BeTrue();
        bounds.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void IntersectsBoxesItTouchesAndNotThoseApart()
    {
        Bounds touching = new(new Vector3(1, 0, 0), new Vector3(2, 1, 1));
        Bounds apart = new(new Vector3(1.5f, 0, 0), new Vector3(2, 1, 1));

        UnitCube.Intersects(touching).Should().BeTrue();
        UnitCube.Intersects(apart).Should().BeFalse();
        UnitCube.Intersects(Bounds.Empty).Should().BeFalse();
    }

    [Fact]
    public void ATransformedBoxHoldsTheTransformedCorners()
    {
        Matrix4x4 turnedAndMoved = Matrix4x4.CreateRotationY(MathF.PI / 4) * Matrix4x4.CreateTranslation(10, 0, 0);

        Bounds placed = UnitCube.Transform(turnedAndMoved);

        float halfDiagonal = MathF.Sqrt(2);
        placed.Min.X.Should().BeApproximately(10 - halfDiagonal, Precision);
        placed.Max.X.Should().BeApproximately(10 + halfDiagonal, Precision);
        placed.Min.Y.Should().BeApproximately(-1, Precision);
        placed.Max.Z.Should().BeApproximately(halfDiagonal, Precision);
    }

    [Fact]
    public void AnEmptyBoxStaysEmptyWhenTransformed()
    {
        Bounds placed = Bounds.Empty.Transform(Matrix4x4.CreateTranslation(1, 2, 3));

        placed.IsEmpty.Should().BeTrue();
    }
}
