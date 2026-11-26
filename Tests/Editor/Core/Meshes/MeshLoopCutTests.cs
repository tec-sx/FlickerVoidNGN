using System.Numerics;

using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class MeshLoopCutTests
{
    private readonly SolidMesh _box = MeshTestsBuilder.Box();

    [Fact]
    public void CuttingTheRingRoundTheBoxAddsAPointOnEachEdgeAndSplitsEachQuad()
    {
        MeshResult? result = MeshLoopCut.Cut(_box, SolidEdge.Of(0, 1), 1, 0);

        result.Should().NotBeNull();
        result!.Solid.Points.Should().HaveCount(12);
        result.Solid.Faces.Should().HaveCount(10);
        result.Edges.Should().HaveCount(4);
        result.Solid.Points.Skip(8).Should().AllSatisfy(point => point.X.Should().BeApproximately(0, 1e-5f));
    }

    [Fact]
    public void SlidingMovesEveryLoopTheSameWayAlongTheRing()
    {
        MeshResult? result = MeshLoopCut.Cut(_box, SolidEdge.Of(0, 1), 1, 0.5f);

        float[] xs = [.. result!.Solid.Points.Skip(8).Select(point => point.X)];
        xs.Should().AllSatisfy(x => x.Should().BeApproximately(xs[0], 1e-5f));
        MathF.Abs(xs[0]).Should().BeApproximately(0.25f, 1e-5f);
    }

    [Fact]
    public void SeveralCutsAreEvenlySpaced()
    {
        MeshResult? result = MeshLoopCut.Cut(_box, SolidEdge.Of(0, 1), 3, 0);

        result!.Solid.Points.Should().HaveCount(20);
        result.Solid.Faces.Should().HaveCount(18);
        result.Edges.Should().HaveCount(12);
        float[] xs = [.. result.Solid.Points.Skip(8).Select(point => point.X).Distinct(new CloseFloats())];
        xs.Should().HaveCount(3);
    }

    [Fact]
    public void ARingAcrossABarOfRingsCutsItsQuadsIntoTwoStillClosed()
    {
        SolidMesh bar = MeshTestsBuilder.Bar(3);

        MeshResult? result = MeshLoopCut.Cut(bar, SolidEdge.Of(0, 4), 1, 0);

        result!.Solid.Points.Should().HaveCount(16);
        result.Solid.Faces.Should().HaveCount(14);
        result.Solid.Points.Skip(12).Should().AllSatisfy(point => point.Y.Should().BeApproximately(0.5f, 1e-5f));
    }

    [Fact]
    public void ARingThatEndsAtAFaceThatIsNotAQuadGivesThatFaceTheExtraPoints()
    {
        SolidMesh cylinder = SolidMesh.Cylinder(1, 1, 5);
        SolidEdge rim = SolidEdge.Of(0, 1);

        MeshResult? result = MeshLoopCut.Cut(cylinder, rim, 1, 0);

        result!.Solid.Points.Should().HaveCount(12);
        result.Solid.Faces.Should().HaveCount(8);
        result.Solid.Faces.Count(face => face.Count == 6).Should().Be(2);
    }

    [Fact]
    public void NoCutsOrAnEdgeTheSolidLacksMakesNothing()
    {
        MeshLoopCut.Cut(_box, SolidEdge.Of(0, 1), 0, 0).Should().BeNull();
        MeshLoopCut.Cut(_box, SolidEdge.Of(0, 7), 1, 0).Should().BeNull();
    }

    private sealed class CloseFloats : IEqualityComparer<float>
    {
        public bool Equals(float x, float y) => MathF.Abs(x - y) < 1e-4f;

        public int GetHashCode(float value) => 0;
    }
}
