using System.Numerics;

using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Runtime.Tests.Geometry;

public sealed class FrustumTests
{
    private static readonly Vector3 HalfUnit = new(0.5f);

    [Fact]
    public void HoldsBoxesInAPerspectiveViewAndNotThoseBesideBehindOrBeyondIt()
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2, 1, 0.1f, 100);
        Frustum frustum = new(view * projection);

        frustum.Intersects(Around(new Vector3(0, 0, -10))).Should().BeTrue();
        frustum.Intersects(Around(new Vector3(-10, 0, -10))).Should().BeTrue("it straddles the left side");
        frustum.Intersects(Around(new Vector3(20, 0, -10))).Should().BeFalse("it lies beside the view");
        frustum.Intersects(Around(new Vector3(0, 0, 10))).Should().BeFalse("it lies behind the camera");
        frustum.Intersects(Around(new Vector3(0, 0, -200))).Should().BeFalse("it lies beyond the far plane");
    }

    [Fact]
    public void HoldsBoxesInAnOrthographicViewAndNotThoseOutsideIt()
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 10), Vector3.Zero, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreateOrthographic(10, 10, 0.1f, 50);
        Frustum frustum = new(view * projection);

        frustum.Intersects(Around(Vector3.Zero)).Should().BeTrue();
        frustum.Intersects(Around(new Vector3(5.2f, 0, 0))).Should().BeTrue("it straddles the right side");
        frustum.Intersects(Around(new Vector3(0, 6, 0))).Should().BeFalse("it lies above the view");
        frustum.Intersects(Around(new Vector3(0, 0, -45))).Should().BeFalse("it lies beyond the far plane");
        frustum.Intersects(Bounds.Empty).Should().BeFalse();
    }

    private static Bounds Around(Vector3 center) => new(center - HalfUnit, center + HalfUnit);
}
