using System.Numerics;

using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class MeshExtrudeTests
{
    private const int Top = 2;

    private readonly SolidMesh _box = MeshTestsBuilder.Box();

    [Fact]
    public void ExtrudingTheTopFaceGrowsAWallAndLiftsTheFace()
    {
        MeshResult? result = MeshExtrude.Faces(_box, new HashSet<int> { Top }, 1);

        result.Should().NotBeNull();
        result!.Solid.Points.Should().HaveCount(12);
        result.Solid.Faces.Should().HaveCount(10);
        result.Solid.Points.Max(point => point.Y).Should().BeApproximately(2, 1e-5f);
        result.Faces.Should().ContainSingle();
        IReadOnlyList<int> lifted = result.Solid.Faces[result.Faces[0]];
        lifted.Select(point => result.Solid.Points[point].Y).Should().AllSatisfy(y => y.Should().BeApproximately(2, 1e-5f));
    }

    [Fact]
    public void ANegativeDistancePushesTheFaceIn()
    {
        MeshResult? result = MeshExtrude.Faces(_box, new HashSet<int> { Top }, -0.25f);

        result!.Solid.Points.Max(point => point.Y).Should().BeApproximately(1, 1e-5f);
        IReadOnlyList<int> pushed = result.Solid.Faces[result.Faces[0]];
        pushed.Select(point => result.Solid.Points[point].Y).Should().AllSatisfy(y => y.Should().BeApproximately(0.75f, 1e-5f));
    }

    [Fact]
    public void ARegionAroundACornerLeavesTheCornerWithoutAFaceAndRemovesIt()
    {
        HashSet<int> corner = [0, 2, 4];

        MeshResult? result = MeshExtrude.Faces(_box, corner, 0.5f);

        result!.Solid.Points.Should().HaveCount(14);
        result.Solid.Faces.Should().HaveCount(12);
        result.Faces.Should().HaveCount(3);
    }

    [Fact]
    public void ExtrudingNothingOrEverythingMakesNothing()
    {
        MeshExtrude.Faces(_box, new HashSet<int>(), 1).Should().BeNull();
        MeshExtrude.Faces(_box, new HashSet<int>(Enumerable.Range(0, 6)), 1).Should().BeNull();
    }

    [Fact]
    public void TheResultIsAClosedSolidFacingOutward()
    {
        MeshResult? result = MeshExtrude.Faces(MeshTestsBuilder.Bar(3), new HashSet<int> { 8 }, 0.5f);

        result.Should().NotBeNull();
        new SolidTopology(result!.Solid).Edges.Should().HaveCount(result.Solid.Points.Count + result.Solid.Faces.Count - 2);
    }

    [Fact]
    public void SeparateRegionsArePushedAlongTheirOwnNormals()
    {
        MeshResult? result = MeshExtrude.Faces(_box, new HashSet<int> { 0, 1 }, 0.5f);

        result!.Solid.Points.Min(point => point.X).Should().BeApproximately(-1, 1e-5f);
        result.Solid.Points.Max(point => point.X).Should().BeApproximately(1, 1e-5f);
    }
}
