using System.Numerics;

using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Runtime.Tests.Geometry;

public sealed class RailTests
{
    private const float Precision = 1e-3f;

    [Fact]
    public void PassesThroughEveryPoint()
    {
        Rail rail = new RailTestsBuilder().Through(0, 0, 0).Through(2, 1, 0).Through(3, 0, 4).Through(-1, 2, 6).Build();

        for (int index = 0; index < rail.Points.Count; index++)
        {
            float distance = rail.GetPointDistance(index);
            Vector3 point = rail.GetPoint(distance);
            Vector3.Distance(point, rail.Points[index]).Should().BeLessThan(Precision, $"point {index} is on the rail");
        }
    }

    [Fact]
    public void MovesAtAnEvenSpeedAlongUnevenlySpacedPoints()
    {
        Rail rail = new RailTestsBuilder().Through(0, 0, 0).Through(1, 0, 0).Through(4, 0, 0).Build();

        rail.Length.Should().BeApproximately(4, Precision);
        rail.GetPointDistance(1).Should().BeApproximately(1, Precision);
        rail.GetPoint(2.5f).X.Should().BeApproximately(2.5f, Precision);
    }

    [Fact]
    public void DoesNotOvershootAHairpin()
    {
        Rail rail = new RailTestsBuilder().Through(0, 0, 0).Through(10, 0, 0).Through(10, 0, 1).Through(0, 0, 1).Build();

        float furthest = 0;
        for (float distance = 0; distance <= rail.Length; distance += 0.05f)
        {
            Vector3 point = rail.GetPoint(distance);
            furthest = MathF.Max(furthest, point.X);
        }

        furthest.Should().BeLessThan(10.25f, "a centripetal spline bulges only a little past a tight turn");
    }

    [Fact]
    public void FindsTheDistanceOfTheNearestPoint()
    {
        Rail rail = new RailTestsBuilder().Through(0, 0, 0).Through(5, 0, 0).Through(10, 0, 0).Build();

        rail.FindNearest(new Vector3(4, 0, 3)).Should().BeApproximately(4, Precision);
        rail.FindNearest(new Vector3(-5, 0, 0)).Should().Be(0);
        rail.FindNearest(new Vector3(20, 1, 0)).Should().BeApproximately(10, Precision);
    }

    [Fact]
    public void ClampsDistancesBeyondItsEnds()
    {
        Rail rail = new RailTestsBuilder().Through(0, 0, 0).Through(5, 0, 0).Build();

        rail.GetPoint(-1).Should().Be(Vector3.Zero);
        Vector3.Distance(rail.GetPoint(100), new Vector3(5, 0, 0)).Should().BeLessThan(Precision);
    }

    [Fact]
    public void AOnePointRailStaysAtItsPoint()
    {
        Rail rail = new RailTestsBuilder().Through(1, 2, 3).Build();

        rail.Length.Should().Be(0);
        rail.GetPoint(5).Should().Be(new Vector3(1, 2, 3));
        rail.FindNearest(Vector3.Zero).Should().Be(0);
    }
}
