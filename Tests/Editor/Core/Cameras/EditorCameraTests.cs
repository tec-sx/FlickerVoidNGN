using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Tests.Cameras;

public sealed class EditorCameraTests
{
    private const float Precision = EditorCameraTestsBuilder.Precision;

    private readonly EditorCameraTestsBuilder _builder = new();

    [Fact]
    public void LooksAtItsPivotFromItsDistance()
    {
        Vector3 pivot = new(1, 2, 3);
        EditorCamera camera = _builder.LookingAt(pivot, 0.5f, -0.4f, 6).Build();

        Vector3.Distance(camera.Pivot, pivot).Should().BeLessThan(Precision);
        Vector3.Distance(camera.Position, pivot).Should().BeApproximately(6, Precision);
    }

    [Fact]
    public void FramesASphereFromWhereItLooks()
    {
        EditorCamera camera = _builder.LookingAt(Vector3.Zero, 0.5f, -0.4f, 30).Build();
        Vector3 center = new(4, 1, -2);

        camera.Frame(center, 2);

        Vector3.Distance(camera.Pivot, center).Should().BeLessThan(Precision);
        camera.Yaw.Should().BeApproximately(0.5f, Precision);
        camera.Pitch.Should().BeApproximately(-0.4f, Precision);
        float halfAngle = float.DegreesToRadians(camera.FieldOfView) / 2;
        camera.PivotDistance.Should().BeGreaterThanOrEqualTo(2 / MathF.Sin(halfAngle));
    }

    [Fact]
    public void FramesASphereWholeInTheOrthographicView()
    {
        EditorCamera camera = _builder.LookingAt(Vector3.Zero, 0, -0.4f, 30).Build();
        camera.Projection = CameraProjection.Orthographic;

        camera.Frame(Vector3.Zero, 3);

        camera.ViewHeight.Should().BeGreaterThanOrEqualTo(6);
        camera.ViewHeight.Should().BeLessThan(9);
    }

    [Fact]
    public void TurnsInPlace()
    {
        EditorCamera camera = _builder.LookingAt(Vector3.Zero, 0, -0.3f, 10).Build();
        Vector3 position = camera.Position;

        camera.Turn(0.4f, -0.2f);

        Vector3.Distance(camera.Position, position).Should().BeLessThan(Precision);
        camera.Yaw.Should().BeApproximately(0.4f, Precision);
        camera.Pitch.Should().BeApproximately(-0.5f, Precision);
    }

    [Fact]
    public void OrbitsRoundItsPivot()
    {
        Vector3 pivot = new(2, 0, -1);
        EditorCamera camera = _builder.LookingAt(pivot, 0, -0.3f, 8).Build();
        Vector3 position = camera.Position;

        camera.Orbit(1, -0.4f);

        Vector3.Distance(camera.Pivot, pivot).Should().BeLessThan(Precision);
        Vector3.Distance(camera.Position, position).Should().BeGreaterThan(0.1f);
        Vector3.Distance(camera.Position, pivot).Should().BeApproximately(8, Precision);
    }

    [Fact]
    public void StopsPitchingShortOfStraightUpOrDown()
    {
        EditorCamera camera = _builder.Build();

        camera.Turn(0, -10);
        float down = camera.Pitch;
        camera.Turn(0, 20);

        down.Should().Be(-EditorCamera.PitchLimit);
        camera.Pitch.Should().Be(EditorCamera.PitchLimit);
    }

    [Fact]
    public void DolliesTowardItsPivot()
    {
        Vector3 pivot = new(0, 1, 0);
        EditorCamera camera = _builder.LookingAt(pivot, 0.3f, -0.5f, 10).Build();

        camera.Dolly(1);

        Vector3.Distance(camera.Pivot, pivot).Should().BeLessThan(Precision);
        camera.PivotDistance.Should().BeLessThan(10);
    }

    [Fact]
    public void DolliesOutAsFarAsItDolliedIn()
    {
        EditorCamera camera = _builder.Build();

        camera.Dolly(3);
        camera.Dolly(-3);

        camera.PivotDistance.Should().BeApproximately(10, Precision);
    }

    [Fact]
    public void StopsDollyingShortOfItsPivot()
    {
        EditorCamera camera = _builder.Build();

        camera.Dolly(1000);

        camera.PivotDistance.Should().Be(EditorCamera.MinPivotDistance);
    }

    [Fact]
    public void PansSoTheGroundUnderThePivotFollowsTheMouse()
    {
        EditorCamera camera = _builder.LookingDown(10).Build();
        float viewHeight = camera.ViewHeight;

        // Dragging right by a whole viewport height carries the ground under the pivot that far right, so the camera moves left.
        camera.Pan(new Vector2(500, 0), 500);

        camera.Pivot.X.Should().BeApproximately(-viewHeight, Precision);
        camera.Pivot.Z.Should().BeApproximately(0, Precision);
    }

    [Fact]
    public void FliesAlongItsViewAtItsSpeed()
    {
        EditorCamera camera = _builder.LookingAt(Vector3.Zero, 0, 0, 10).Build();
        camera.FlySpeed = 4;

        camera.Fly(new Vector3(0, 0, 1), 0.5f);

        Vector3.Distance(camera.Position, new Vector3(0, 0, 8)).Should().BeLessThan(Precision);
    }

    [Fact]
    public void FliesUpAlongTheWorldsUpRatherThanItsOwn()
    {
        EditorCamera camera = _builder.LookingAt(Vector3.Zero, 0, -0.8f, 10).Build();
        Vector3 position = camera.Position;
        camera.FlySpeed = 2;

        camera.Fly(new Vector3(0, 1, 0), 1);

        Vector3.Distance(camera.Position, position + new Vector3(0, 2, 0)).Should().BeLessThan(Precision);
    }

    [Fact]
    public void FramesTheSameHeightAtThePivotInBothProjections()
    {
        EditorCamera camera = _builder.LookingAt(Vector3.Zero, 0, 0, 10).Build();
        camera.FieldOfView = 90;
        camera.Projection = CameraProjection.Orthographic;

        Camera view = camera.ToCamera();

        view.Projection.Should().Be(CameraProjection.Orthographic);
        view.FieldOfView.Should().Be(90);
        view.OrthographicHeight.Should().BeApproximately(20, Precision);
    }

    [Fact]
    public void StandsAtItsPositionWhenPerspective()
    {
        EditorCamera camera = _builder.LookingAt(Vector3.Zero, 0.2f, -0.6f, 10).Build();

        Transform transform = camera.ToTransform();

        Vector3.Distance(transform.Position, camera.Position).Should().BeLessThan(Precision);
        transform.Rotation.Should().Be(camera.Rotation);
    }

    [Fact]
    public void BacksAwayFromThePivotWhenOrthographicSoNothingNearItIsClipped()
    {
        EditorCamera camera = _builder.LookingAt(Vector3.Zero, 0, 0, 1).Build();
        camera.Projection = CameraProjection.Orthographic;

        Transform transform = camera.ToTransform();
        Camera view = camera.ToCamera();

        float eyeDistance = transform.Position.Length();
        eyeDistance.Should().BeApproximately(view.FarPlane / 2, Precision);
        transform.Rotation.Should().Be(camera.Rotation);
    }

    [Fact]
    public void CastsRaysThroughThePixelUnderTheMouse()
    {
        EditorCamera camera = _builder.LookingAt(new Vector3(1, 0, 2), 0.4f, -0.5f, 10).Build();

        Ray ray = camera.GetRay(new Vector2(320, 240), 640, 480);

        float distanceToPivot = Vector3.Cross(camera.Pivot - ray.Origin, ray.Direction).Length();
        distanceToPivot.Should().BeLessThan(0.01f, "the middle of the viewport looks at the pivot");
        ray.Direction.Length().Should().BeApproximately(1, Precision);
    }

    [Theory]
    [InlineData(CameraProjection.Perspective)]
    [InlineData(CameraProjection.Orthographic)]
    public void ProjectsPointsToThePixelsTheirRaysPassThrough(CameraProjection projection)
    {
        EditorCamera camera = _builder.LookingAt(Vector3.Zero, 0.4f, -0.5f, 10).Build();
        camera.Projection = projection;
        Vector3 point = new(1.5f, 0.5f, -1);

        Vector2 pixel = camera.Project(point, 640, 480);
        Ray ray = camera.GetRay(pixel, 640, 480);

        float distance = Vector3.Cross(point - ray.Origin, ray.Direction).Length();
        distance.Should().BeLessThan(0.01f);
    }

    [Fact]
    public void MeasuresHowManyWorldUnitsAPixelCoversAtAPoint()
    {
        EditorCamera camera = _builder.LookingAt(Vector3.Zero, 0, 0, 10).Build();

        float atPivot = camera.WorldUnitsPerPixel(Vector3.Zero, 500);
        float twiceAsFar = camera.WorldUnitsPerPixel(new Vector3(0, 0, -10), 500);

        atPivot.Should().BeApproximately(camera.ViewHeight / 500, Precision);
        twiceAsFar.Should().BeApproximately(2 * atPivot, Precision);
    }
}
