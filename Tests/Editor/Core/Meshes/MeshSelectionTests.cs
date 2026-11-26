using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class MeshSelectionTests
{
    private static MeshSelection Select(SolidMesh mesh) => new(mesh);

    [Fact]
    public void StartsWithNothingSelectedInPointMode()
    {
        MeshSelection selection = Select(MeshTestsBuilder.Box());

        selection.Element.Should().Be(MeshElement.Point);
        selection.Points.Should().BeEmpty();
        selection.Edges.Should().BeEmpty();
        selection.Faces.Should().BeEmpty();
    }

    [Fact]
    public void AClickSelectsOnePointAndReplacesTheLastOne()
    {
        MeshSelection selection = Select(MeshTestsBuilder.Box());

        selection.Select(MeshHit.OfPoint(2), additive: false);
        selection.Select(MeshHit.OfPoint(5), additive: false);

        selection.Points.Should().BeEquivalentTo([5]);
    }

    [Fact]
    public void ShiftClickAddsAndTakesAwayAPoint()
    {
        MeshSelection selection = Select(MeshTestsBuilder.Box());

        selection.Select(MeshHit.OfPoint(2), additive: false);
        selection.Select(MeshHit.OfPoint(5), additive: true);
        selection.Select(MeshHit.OfPoint(2), additive: true);

        selection.Points.Should().BeEquivalentTo([5]);
    }

    [Fact]
    public void ClickingNothingClearsTheSelection()
    {
        MeshSelection selection = Select(MeshTestsBuilder.Box());
        selection.Select(MeshHit.OfPoint(2), additive: false);

        selection.Select(null, additive: false);

        selection.Points.Should().BeEmpty();
    }

    [Fact]
    public void ShiftClickingNothingKeepsTheSelection()
    {
        MeshSelection selection = Select(MeshTestsBuilder.Box());
        selection.Select(MeshHit.OfPoint(2), additive: false);

        selection.Select(null, additive: true);

        selection.Points.Should().BeEquivalentTo([2]);
    }

    [Fact]
    public void SelectingEveryPointOfAFaceSelectsItsEdgesAndTheFace()
    {
        SolidMesh box = MeshTestsBuilder.Box();
        MeshSelection selection = Select(box);

        foreach (int point in box.Faces[4])
        {
            selection.Select(MeshHit.OfPoint(point), additive: true);
        }

        selection.Faces.Should().BeEquivalentTo([4]);
        selection.Edges.Should().HaveCount(4);
    }

    [Fact]
    public void AnEdgeSelectsItsTwoPoints()
    {
        MeshSelection selection = Select(MeshTestsBuilder.Box());
        selection.SetElement(MeshElement.Edge);

        selection.Select(MeshHit.OfEdge(SolidEdge.Of(6, 7)), additive: false);

        selection.Edges.Should().BeEquivalentTo([SolidEdge.Of(6, 7)]);
        selection.Points.Should().BeEquivalentTo([6, 7]);
    }

    [Fact]
    public void AFaceSelectsItsEdgesAndPoints()
    {
        SolidMesh box = MeshTestsBuilder.Box();
        MeshSelection selection = Select(box);
        selection.SetElement(MeshElement.Face);

        selection.Select(MeshHit.OfFace(4, SolidEdge.Of(4, 5)), additive: false);

        selection.Faces.Should().BeEquivalentTo([4]);
        selection.Edges.Should().HaveCount(4);
        selection.Points.Should().BeEquivalentTo(box.Faces[4]);
    }

    [Fact]
    public void ChangingFromPointsToEdgesKeepsTheEdgesBetweenSelectedPoints()
    {
        MeshSelection selection = Select(MeshTestsBuilder.Box());
        selection.Select(MeshHit.OfPoint(6), additive: false);
        selection.Select(MeshHit.OfPoint(7), additive: true);
        selection.Select(MeshHit.OfPoint(0), additive: true);

        selection.SetElement(MeshElement.Edge);

        selection.Edges.Should().BeEquivalentTo([SolidEdge.Of(6, 7)]);
    }

    [Fact]
    public void ChangingFromFacesToPointsKeepsTheFacesPoints()
    {
        SolidMesh box = MeshTestsBuilder.Box();
        MeshSelection selection = Select(box);
        selection.SetElement(MeshElement.Face);
        selection.Select(MeshHit.OfFace(4, SolidEdge.Of(4, 5)), additive: false);

        selection.SetElement(MeshElement.Point);

        selection.Points.Should().BeEquivalentTo(box.Faces[4]);
        selection.Faces.Should().BeEquivalentTo([4]);
    }

    [Fact]
    public void ChangingFromEdgesToFacesKeepsOnlyFacesWhoseEdgesAreAllSelected()
    {
        SolidMesh box = MeshTestsBuilder.Box();
        MeshSelection selection = Select(box);
        selection.SetElement(MeshElement.Edge);
        foreach (SolidEdge edge in new SolidTopology(box).GetFaceEdges(4))
        {
            selection.Select(MeshHit.OfEdge(edge), additive: true);
        }

        selection.Select(MeshHit.OfEdge(SolidEdge.Of(0, 1)), additive: true);
        selection.SetElement(MeshElement.Face);

        selection.Faces.Should().BeEquivalentTo([4]);
    }

    [Fact]
    public void SelectAllSelectsEveryElementOfTheModeAndClearTakesThemAway()
    {
        SolidMesh box = MeshTestsBuilder.Box();
        MeshSelection selection = Select(box);

        selection.SelectAll();

        selection.Points.Should().HaveCount(8);
        selection.Edges.Should().HaveCount(12);
        selection.Faces.Should().HaveCount(6);

        selection.Clear();

        selection.Points.Should().BeEmpty();
        selection.Faces.Should().BeEmpty();
    }

    [Fact]
    public void AltClickingAnEdgeSelectsItsLoop()
    {
        SolidMesh bar = MeshTestsBuilder.Bar(3);
        MeshSelection selection = Select(bar);
        selection.SetElement(MeshElement.Edge);

        selection.SelectLoop(MeshHit.OfEdge(SolidEdge.Of(4, 5)), additive: false);

        selection.Edges.Should().BeEquivalentTo([SolidEdge.Of(4, 5), SolidEdge.Of(5, 6), SolidEdge.Of(6, 7), SolidEdge.Of(4, 7)]);
    }

    [Fact]
    public void AltShiftClickingALoopAddsItToTheSelection()
    {
        MeshSelection selection = Select(MeshTestsBuilder.Bar(3));
        selection.SetElement(MeshElement.Edge);
        selection.Select(MeshHit.OfEdge(SolidEdge.Of(0, 1)), additive: false);

        selection.SelectLoop(MeshHit.OfEdge(SolidEdge.Of(4, 5)), additive: true);

        selection.Edges.Should().Contain(SolidEdge.Of(0, 1)).And.HaveCount(5);
    }

    [Fact]
    public void AltClickingAFaceSelectsTheStripAcrossTheEdgeNearestTheMouse()
    {
        SolidMesh bar = MeshTestsBuilder.Bar(4);
        MeshSelection selection = Select(bar);
        selection.SetElement(MeshElement.Face);

        selection.SelectLoop(MeshHit.OfFace(4, SolidEdge.Of(4, 8)), additive: false);

        selection.Faces.Should().BeEquivalentTo([4, 5, 6, 7]);
    }
}
