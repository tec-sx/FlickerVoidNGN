using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Shadows;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class ShadowProjectionsTests
{
    private static readonly Vector3 Slanted = Vector3.Normalize(new Vector3(-0.4f, -0.8f, -0.45f));

    private readonly ShadowProjectionsTestsBuilder _builder = new();

    [Theory]
    [InlineData(CameraProjection.Orthographic)]
    [InlineData(CameraProjection.Perspective)]
    public void FitsTheSunShadowAroundTheViewUpToTheShadowDistance(CameraProjection projection)
    {
        Matrix4x4 shadow = _builder.WithProjection(projection).FitSun(Slanted);

        IEnumerable<Vector3> corners = _builder.ViewCorners();

        foreach (Vector3 corner in corners)
        {
            Vector3 projected = ShadowProjectionsTestsBuilder.Project(shadow, corner);
            projected.X.Should().BeInRange(-1, 1);
            projected.Y.Should().BeInRange(-1, 1);
            projected.Z.Should().BeInRange(0, 1);
        }
    }

    [Fact]
    public void ReachesTowardTheSunForCastersOutOfView()
    {
        Matrix4x4 shadow = _builder.FitSun(-Vector3.UnitY);

        Vector3 above = ShadowProjectionsTestsBuilder.Project(shadow, new Vector3(0, ShadowProjectionsTestsBuilder.CasterReach * 0.9f, 0));

        above.Z.Should().BeInRange(0, 1);
    }

    [Fact]
    public void MovesTheSunShadowInWholeTexels()
    {
        Matrix4x4 before = _builder.FitSun(Slanted);
        Matrix4x4 after = _builder.WithCameraOffset(new Vector3(0.0123f, 0.0456f, 0)).FitSun(Slanted);

        Vector3 origin = Vector3.Zero;
        Vector3 projectedBefore = ShadowProjectionsTestsBuilder.Project(before, origin);
        Vector3 projectedAfter = ShadowProjectionsTestsBuilder.Project(after, origin);

        float texels = (projectedAfter.X - projectedBefore.X) * ShadowAtlas.SunTileSize / 2;
        texels.Should().BeApproximately(MathF.Round(texels), 1e-2f);
    }

    [Fact]
    public void FitsTheSunShadowOnlyToTheWorldTheViewSees()
    {
        Bounds world = new(new Vector3(-2), new Vector3(2));
        Matrix4x4 loose = _builder.FitSun(Slanted);
        Matrix4x4 tight = _builder.WithWorld(world).FitSun(Slanted);

        for (int index = 0; index < 8; index++)
        {
            Vector3 corner = new((index & 1) == 0 ? -2 : 2, (index & 2) == 0 ? -2 : 2, (index & 4) == 0 ? -2 : 2);
            Vector3 projected = ShadowProjectionsTestsBuilder.Project(tight, corner);
            projected.X.Should().BeInRange(-1, 1);
            projected.Y.Should().BeInRange(-1, 1);
            projected.Z.Should().BeInRange(0, 1);
        }

        float looseDetail = ShadowProjectionsTestsBuilder.Detail(loose);
        float tightDetail = ShadowProjectionsTestsBuilder.Detail(tight);
        tightDetail.Should().BeGreaterThan(2 * looseDetail, "a world smaller than the view gets the map's texels to itself");
    }

    [Fact]
    public void FitsTheSunShadowToWhereTheViewCutsTheWorld()
    {
        Bounds ground = new(new Vector3(-1000, -1000, -1), new Vector3(1000, 1000, 0));
        Matrix4x4 tight = _builder.WithWorld(ground).FitSun(-Vector3.UnitZ);
        Vector3[] corners = [.. _builder.ViewCorners()];
        Vector3 center = corners.Aggregate(Vector3.Zero, static (sum, corner) => sum + corner) / corners.Length;
        float radius = corners.Max(corner => Vector3.Distance(corner, center));

        Vector3 corner = ShadowProjectionsTestsBuilder.Project(tight, new Vector3(8.8f, 4.9f, -0.5f));
        corner.X.Should().BeInRange(-1, 1, "the ground the view sees is in the map");
        corner.Y.Should().BeInRange(-1, 1);
        float sphereDetail = ShadowAtlas.SunTileSize / (2 * radius);
        float tightDetail = ShadowProjectionsTestsBuilder.Detail(tight);
        tightDetail.Should().BeGreaterThan(1.2f * sphereDetail, "the map no longer wraps a sphere round the whole depth of the view");
    }

    [Fact]
    public void HasNoSunShadowWhereTheViewMissesTheWorld()
    {
        Bounds away = new(new Vector3(500), new Vector3(510));

        Matrix4x4? shadow = _builder.WithWorld(away).TryFitSun(Slanted);

        shadow.Should().BeNull();
    }

    [Fact]
    public void KeepsTheSunShadowStillWhileTheCameraMovesALittle()
    {
        Matrix4x4 before = _builder.FitSun(Slanted);
        Matrix4x4 after = _builder.WithCameraOffset(new Vector3(0.05f, 0.05f, 0)).FitSun(Slanted);

        after.Should().Be(before, "the map moves in steps, so a kept shadow tile stays good while the camera moves within one");
    }

    [Fact]
    public void FollowsTheCameraWhenItMovesFurther()
    {
        Matrix4x4 before = _builder.FitSun(Slanted);
        Matrix4x4 after = _builder.WithCameraOffset(new Vector3(5, 0, 0)).FitSun(Slanted);

        Vector3 corner = ShadowProjectionsTestsBuilder.Project(after, new Vector3(13.8f, 4.9f, 0));
        after.Should().NotBe(before);
        corner.X.Should().BeInRange(-1, 1, "the moved view's corner is in the map");
    }

    [Fact]
    public void SeesASpotLightsConeUpToItsRange()
    {
        Matrix4x4 shadow = ShadowProjections.Spot(Vector3.Zero, Quaternion.Identity, outerConeAngle: 30, range: 4);

        Vector3 onAxis = ShadowProjectionsTestsBuilder.Project(shadow, new Vector3(0, 0, -2));
        Vector3 outside = ShadowProjectionsTestsBuilder.Project(shadow, new Vector3(2, 0, -2));

        onAxis.X.Should().BeApproximately(0, 1e-5f);
        onAxis.Y.Should().BeApproximately(0, 1e-5f);
        onAxis.Z.Should().BeInRange(0, 1);
        outside.X.Should().BeGreaterThan(1, "45 degrees off the axis is outside a 30 degree cone");
    }

    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(1, -1, 0, 0)]
    [InlineData(2, 0, 1, 0)]
    [InlineData(3, 0, -1, 0)]
    [InlineData(4, 0, 0, 1)]
    [InlineData(5, 0, 0, -1)]
    public void LooksDownEachAxisFromAPointLight(int face, float x, float y, float z)
    {
        Matrix4x4 shadow = ShadowProjections.PointFace(Vector3.Zero, face, range: 4);

        Vector3 projected = ShadowProjectionsTestsBuilder.Project(shadow, new Vector3(x, y, z));

        projected.X.Should().BeApproximately(0, 1e-5f);
        projected.Y.Should().BeApproximately(0, 1e-5f);
        projected.Z.Should().BeInRange(0, 1);
        ShadowProjections.GetPointFace(new Vector3(x, y, z)).Should().Be(face);
    }

    [Fact]
    public void MapsClipSpaceOntoTheAtlasTile()
    {
        ShadowTile tile = new(1024, 512, 512);
        Matrix4x4 toTile = ShadowProjections.ToAtlasTile(tile);

        Vector3 topLeft = ShadowProjectionsTestsBuilder.Project(toTile, new Vector3(-1, 1, 0.5f));
        Vector3 bottomRight = ShadowProjectionsTestsBuilder.Project(toTile, new Vector3(1, -1, 0.5f));

        topLeft.X.Should().BeApproximately(0.25f, 1e-6f);
        topLeft.Y.Should().BeApproximately(0.125f, 1e-6f);
        bottomRight.X.Should().BeApproximately(0.375f, 1e-6f);
        bottomRight.Y.Should().BeApproximately(0.25f, 1e-6f);
        topLeft.Z.Should().BeApproximately(0.5f, 1e-6f);
    }
}
