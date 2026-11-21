using System.Numerics;

using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Runtime.Tests.Geometry;

public sealed class RayTests
{
    private const float Precision = 1e-4f;

    private static readonly Ray DownTheZAxis = new(new Vector3(0, 0, 10), -Vector3.UnitZ);

    [Fact]
    public void HitsATriangleItPassesThrough()
    {
        bool hit = DownTheZAxis.IntersectTriangle(new Vector3(-1, -1, 2), new Vector3(1, -1, 2), new Vector3(0, 1, 2), out float distance);

        hit.Should().BeTrue();
        distance.Should().BeApproximately(8, Precision);
    }

    [Fact]
    public void MissesATriangleBesideIt()
    {
        bool hit = DownTheZAxis.IntersectTriangle(new Vector3(2, -1, 0), new Vector3(4, -1, 0), new Vector3(3, 1, 0), out _);

        hit.Should().BeFalse();
    }

    [Fact]
    public void MissesATriangleBehindItsOrigin()
    {
        bool hit = DownTheZAxis.IntersectTriangle(new Vector3(-1, -1, 12), new Vector3(1, -1, 12), new Vector3(0, 1, 12), out _);

        hit.Should().BeFalse();
    }

    [Fact]
    public void HitsABoxAtItsNearSide()
    {
        bool hit = DownTheZAxis.IntersectBox(new Vector3(-1, -1, -1), new Vector3(1, 1, 1), out float distance);

        hit.Should().BeTrue();
        distance.Should().BeApproximately(9, Precision);
    }

    [Fact]
    public void HitsASphereAtItsNearSide()
    {
        bool hit = DownTheZAxis.IntersectSphere(new Vector3(0, 0, 3), 0.5f, out float distance);

        hit.Should().BeTrue();
        distance.Should().BeApproximately(6.5f, Precision);
    }

    [Fact]
    public void HitsAPlaneFacingIt()
    {
        Ray ray = new(new Vector3(1, 5, 2), -Vector3.UnitY);

        bool hit = ray.IntersectPlane(Vector3.Zero, Vector3.UnitY, out Vector3 point);

        hit.Should().BeTrue();
        Vector3.Distance(point, new Vector3(1, 0, 2)).Should().BeLessThan(Precision);
    }

    [Fact]
    public void FindsWhereALineComesClosestToIt()
    {
        // The ray passes over the X axis at x = 3.
        Ray ray = new(new Vector3(3, 5, 0), -Vector3.UnitY);

        bool found = ray.ClosestOnLine(Vector3.Zero, Vector3.UnitX, out float along);

        found.Should().BeTrue();
        along.Should().BeApproximately(3, Precision);
    }

    [Fact]
    public void FindsNoClosestPointOnALineAlongIt()
    {
        bool found = DownTheZAxis.ClosestOnLine(Vector3.Zero, Vector3.UnitZ, out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void ARayFromTheViewportCentreRunsDownTheViewDirection()
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0, 10, 0), Vector3.Zero, -Vector3.UnitZ);
        Matrix4x4 projection = Matrix4x4.CreateOrthographic(8, 6, 0.1f, 100);
        Matrix4x4 viewProjection = view * projection;
        Vector2 centre = new(400, 300);
        Vector2 rightEdge = new(800, 300);

        Ray middle = Ray.FromViewProjection(viewProjection, centre, 800, 600);
        Ray right = Ray.FromViewProjection(viewProjection, rightEdge, 800, 600);

        middle.Direction.Y.Should().BeApproximately(-1, Precision);
        middle.Origin.X.Should().BeApproximately(0, Precision);
        right.Origin.X.Should().BeApproximately(4, Precision);
    }
}
