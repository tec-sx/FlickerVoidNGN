using System.Numerics;

using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class MeshMirrorTests
{
    private readonly SolidMesh _box = MeshTestsBuilder.Box();

    [Fact]
    public void EachPointOfABoxHasItsTwinAcrossTheCentreLine()
    {
        int[] twins = MeshMirror.FindTwins(_box);

        twins[0].Should().Be(1);
        twins[1].Should().Be(0);
        twins.Should().NotContain(-1);
    }

    [Fact]
    public void ALopsidedSolidHasPointsWithoutTwins()
    {
        SolidMesh lopsided = MeshEdit.Move(_box, new HashSet<int> { 0 }, new Vector3(0.1f, 0, 0));

        int[] twins = MeshMirror.FindTwins(lopsided);

        twins[0].Should().Be(-1);
    }

    [Fact]
    public void ExpandingAFaceAddsTheFaceAcrossAndLeavesTheCentreFacesAlone()
    {
        // Face 0 is the +X side, face 1 the -X side, face 2 the top, which is its own twin.
        MeshMirror.ExpandFaces(_box, new HashSet<int> { 0 }).Should().BeEquivalentTo(new[] { 0, 1 });
        MeshMirror.ExpandFaces(_box, new HashSet<int> { 2 }).Should().BeEquivalentTo(new[] { 2 });
    }

    [Fact]
    public void ExpandingAnEdgeAddsTheEdgeAcross()
    {
        HashSet<SolidEdge> edges = [SolidEdge.Of(0, 2)];

        HashSet<SolidEdge> expanded = MeshMirror.ExpandEdges(_box, edges);

        expanded.Should().BeEquivalentTo(new[] { SolidEdge.Of(0, 2), SolidEdge.Of(1, 3) });
    }

    [Fact]
    public void AMovedPointDragsItsTwinAndAPointOnTheCentreLineStaysOnIt()
    {
        HashSet<int> moved = [1];
        SolidMesh edited = MeshEdit.Move(_box, moved, new Vector3(0.2f, 0.3f, 0));

        SolidMesh followed = MeshMirror.Follow(_box, edited, moved);

        followed.Points[1].Should().Be(edited.Points[1]);
        followed.Points[0].X.Should().BeApproximately(-followed.Points[1].X, 1e-5f);
        followed.Points[0].Y.Should().BeApproximately(followed.Points[1].Y, 1e-5f);
    }

    [Fact]
    public void TwinsThatWereBothMovedAreLeftAsTheEditMadeThem()
    {
        HashSet<int> moved = [0, 1];
        SolidMesh edited = MeshEdit.Move(_box, moved, new Vector3(0, 0.5f, 0));

        SolidMesh followed = MeshMirror.Follow(_box, edited, moved);

        followed.Points.Should().Equal(edited.Points);
    }
}
