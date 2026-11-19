using System.Numerics;

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
