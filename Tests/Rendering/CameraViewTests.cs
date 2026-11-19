using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class CameraViewTests
{
    private readonly CameraViewTestsBuilder _builder = new();

    [Fact]
    public void SeesTheHeightOfTheViewAcrossTheScreen()
    {
        CameraView view = _builder.AtDistance(5).Build();

        Vector3 top = CameraViewTestsBuilder.Project(view, new Vector3(0, 5, 0));

        top.Y.Should().BeApproximately(1, 1e-5f);
    }

    [Fact]
    public void WidensTheViewWithTheAspectRatio()
    {
        CameraView view = _builder.AtDistance(5).WithViewport(200, 100).Build();

        Vector3 right = CameraViewTestsBuilder.Project(view, new Vector3(10, 0, 0));

        right.X.Should().BeApproximately(1, 1e-5f);
    }

    [Fact]
    public void MapsTheNearAndFarPlanesToTheDepthRange()
    {
        CameraView view = _builder.AtDistance(5).Build();

        Vector3 near = CameraViewTestsBuilder.Project(view, new Vector3(0, 0, 5 - CameraViewTestsBuilder.Near));
        Vector3 far = CameraViewTestsBuilder.Project(view, new Vector3(0, 0, 5 - CameraViewTestsBuilder.Far));

        near.Z.Should().BeApproximately(0, 1e-5f);
        far.Z.Should().BeApproximately(1, 1e-5f);
    }

    [Fact]
    public void LooksAlongItsRotatedForwardAxis()
    {
        Quaternion lookDown = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2);
        CameraView view = _builder.At(new Vector3(0, 5, 0), lookDown).Build();

        Vector3 below = CameraViewTestsBuilder.Project(view, Vector3.Zero);

        below.X.Should().BeApproximately(0, 1e-5f);
        below.Y.Should().BeApproximately(0, 1e-5f);
        below.Z.Should().BeInRange(0, 1);
    }

    [Fact]
    public void SnapsItsPositionToWholePixels()
    {
        CameraView view = _builder.At(new Vector3(1.004f, -2.996f, 5), Quaternion.Identity).WithViewport(1000, 1000).Build();

        view.View.Translation.X.Should().BeApproximately(-1, 1e-4f);
        view.View.Translation.Y.Should().BeApproximately(3, 1e-4f);
    }

    [Fact]
    public void SeesTheFieldOfViewThroughAPerspectiveCamera()
    {
        CameraView view = _builder.AtDistance(5).WithPerspective(fieldOfViewDegrees: 90).Build();

        Vector4 top = CameraViewTestsBuilder.ProjectHomogeneous(view, new Vector3(0, 5, 0));

        (top.Y / top.W).Should().BeApproximately(1, 1e-5f, "a 90 degree view sees as high as it is far");
    }

    [Fact]
    public void MapsTheNearAndFarPlanesToTheDepthRangeInPerspective()
    {
        CameraView view = _builder.AtDistance(5).WithPerspective(fieldOfViewDegrees: 60).Build();

        Vector4 near = CameraViewTestsBuilder.ProjectHomogeneous(view, new Vector3(0, 0, 5 - CameraViewTestsBuilder.Near));
        Vector4 far = CameraViewTestsBuilder.ProjectHomogeneous(view, new Vector3(0, 0, 5 - CameraViewTestsBuilder.Far));

        (near.Z / near.W).Should().BeApproximately(0, 1e-5f);
        (far.Z / far.W).Should().BeApproximately(1, 1e-5f);
    }

    [Fact]
    public void DoesNotSnapAPerspectiveCamera()
    {
        CameraView view = _builder.At(new Vector3(1.004f, 0, 5), Quaternion.Identity).WithViewport(1000, 1000).WithPerspective(60).Build();

        view.View.Translation.X.Should().BeApproximately(-1.004f, 1e-5f);
    }
}
