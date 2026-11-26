using System.Numerics;

using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class MeshBevelTests
{
    private readonly SolidMesh _box = MeshTestsBuilder.Box();

    [Fact]
    public void ChamferingOneEdgeOfABoxAddsAStripAndCutsTheCornersOfTheFacesAtItsEnds()
    {
        MeshResult? result = MeshBevel.Edges(_box, Edges(SolidEdge.Of(0, 1)), 0.2f, 1, round: false);

        result.Should().NotBeNull();
        result!.Solid.Points.Should().HaveCount(10);
        result.Solid.Faces.Should().HaveCount(7);
        result.Faces.Should().ContainSingle();
        result.Solid.Faces.Count(face => face.Count == 5).Should().Be(2);
    }

    [Fact]
    public void TheStripIsTheWidthOnEachFace()
    {
        MeshResult? result = MeshBevel.Edges(_box, Edges(SolidEdge.Of(0, 1)), 0.2f, 1, round: false);

        IReadOnlyList<int> strip = result!.Solid.Faces[result.Faces[0]];
        Vector3[] corners = [.. strip.Select(point => result.Solid.Points[point])];
        float width = Vector3.Distance(corners[1], corners[2]);
        width.Should().BeApproximately(0.2f * MathF.Sqrt(2), 1e-4f);
    }

    [Fact]
    public void ManyRowsMakeManyStripsAndRoundingBendsThemTowardTheOldEdge()
    {
        MeshResult? flat = MeshBevel.Edges(_box, Edges(SolidEdge.Of(0, 1)), 0.2f, 2, round: false);
        MeshResult? rounded = MeshBevel.Edges(_box, Edges(SolidEdge.Of(0, 1)), 0.2f, 2, round: true);

        flat!.Solid.Faces.Should().HaveCount(8);
        flat.Faces.Should().HaveCount(2);
        float flatReach = ReachFrom(flat, new Vector3(-0.5f, 0, -0.5f));
        float roundedReach = ReachFrom(rounded!, new Vector3(-0.5f, 0, -0.5f));
        roundedReach.Should().BeLessThan(flatReach);
    }

    [Fact]
    public void BevelingEveryEdgeOfABoxCapsEachCornerWithATriangle()
    {
        HashSet<SolidEdge> all = [.. new SolidTopology(_box).Edges];

        MeshResult? result = MeshBevel.Edges(_box, all, 0.1f, 1, round: false);

        result!.Solid.Points.Should().HaveCount(24);
        result.Solid.Faces.Should().HaveCount(26);
        result.Faces.Should().HaveCount(12);
    }

    [Fact]
    public void TwoEdgesMeetingAtACornerStayClosed()
    {
        MeshResult? result = MeshBevel.Edges(_box, Edges(SolidEdge.Of(0, 1), SolidEdge.Of(1, 3)), 0.15f, 2, round: true);

        result.Should().NotBeNull();
        result!.Faces.Should().HaveCount(4);
    }

    [Fact]
    public void ALoopRoundABarGoesThroughPointsWithFourEdges()
    {
        SolidMesh bar = MeshTestsBuilder.Bar(3);

        MeshResult? result = MeshBevel.Edges(bar, Edges(SolidEdge.Of(4, 5), SolidEdge.Of(5, 6), SolidEdge.Of(6, 7), SolidEdge.Of(4, 7)), 0.1f, 1, round: false);

        result!.Solid.Points.Should().HaveCount(16);
        result.Solid.Faces.Should().HaveCount(14);
    }

    [Fact]
    public void AStripEndingAtAPointWithFourEdgesIsRefused()
    {
        MeshResult? cut = MeshLoopCut.Cut(_box, SolidEdge.Of(0, 1), 1, 0);
        SolidTopology topology = new(cut!.Solid);
        int busy = Enumerable.Range(0, cut.Solid.Points.Count).First(point => topology.GetPointEdges(point).Count == 4);
        SolidEdge edge = topology.GetPointEdges(busy)[0];

        MeshResult? result = MeshBevel.Edges(cut.Solid, Edges(edge), 0.05f, 1, round: false);

        result.Should().BeNull();
    }

    [Fact]
    public void NothingSelectedOrNoWidthMakesNothing()
    {
        MeshBevel.Edges(_box, new HashSet<SolidEdge>(), 0.1f, 1, round: false).Should().BeNull();
        MeshBevel.Edges(_box, Edges(SolidEdge.Of(0, 1)), 0, 1, round: false).Should().BeNull();
        MeshBevel.Edges(_box, Edges(SolidEdge.Of(0, 1)), 0.1f, 0, round: false).Should().BeNull();
    }

    private static HashSet<SolidEdge> Edges(params SolidEdge[] edges) => [.. edges];

    // The middle strip point nearest the given old point.
    private static float ReachFrom(MeshResult result, Vector3 corner)
    {
        float best = float.MaxValue;
        foreach (int face in result.Faces)
        {
            foreach (int point in result.Solid.Faces[face])
            {
                Vector3 position = result.Solid.Points[point];
                best = MathF.Min(best, MathF.Abs(position.Y - corner.Y) + MathF.Abs(position.Z - corner.Z));
            }
        }

        return best;
    }
}
