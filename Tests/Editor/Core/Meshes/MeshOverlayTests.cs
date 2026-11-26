using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class MeshOverlayTests
{
    private readonly SolidMesh _box = MeshTestsBuilder.Box();
    private readonly MeshSelection _selection;
    private readonly EditorCamera _camera = MeshTestsBuilder.CornerCamera();

    public MeshOverlayTests()
    {
        _selection = new MeshSelection(_box);
    }

    [Fact]
    public void DrawsTheEdgesOfTheFacesTurnedTowardTheCameraOnly()
    {
        OverlayBuilder overlay = Draw(hover: null);

        overlay.Vertices.ToArray().Where(vertex => vertex.Color == MeshOverlay.EdgeColor).Should().NotBeEmpty();
        CountColor(overlay, MeshOverlay.EdgeColor).Should().Be(9 * 6);
    }

    [Fact]
    public void PointsShowOnlyInPointMode()
    {
        _selection.SetElement(MeshElement.Edge);
        OverlayBuilder edges = Draw(hover: null);
        _selection.SetElement(MeshElement.Point);
        OverlayBuilder points = Draw(hover: null);

        CountColor(edges, MeshOverlay.PointColor).Should().Be(0);
        CountColor(points, MeshOverlay.PointColor).Should().Be(7 * 36);
    }

    [Fact]
    public void SelectedFacesAreFilledAndTheirEdgesBrightened()
    {
        _selection.SetElement(MeshElement.Face);
        _selection.Select(MeshHit.OfFace(FacingFace, default), additive: false);

        OverlayBuilder overlay = Draw(hover: null);

        CountColor(overlay, MeshOverlay.FaceFill).Should().Be(6);
        CountColor(overlay, MeshOverlay.SelectedColor).Should().BeGreaterThan(0);
    }

    [Fact]
    public void ASelectedEdgeIsDrawnInTheSelectionColor()
    {
        _selection.SetElement(MeshElement.Edge);
        _selection.Select(MeshHit.OfEdge(FacingEdge), additive: false);

        OverlayBuilder overlay = Draw(hover: null);

        CountColor(overlay, MeshOverlay.SelectedColor).Should().Be(6);
    }

    [Fact]
    public void TheHoveredElementIsDrawnInTheHoverColor()
    {
        OverlayBuilder edge = Draw(MeshHit.OfEdge(FacingEdge));
        OverlayBuilder face = Draw(MeshHit.OfFace(FacingFace, FacingEdge));
        OverlayBuilder point = Draw(MeshHit.OfPoint(FacingPoint));

        CountColor(edge, MeshOverlay.HoverColor).Should().Be(6);
        CountColor(face, MeshOverlay.HoverFill).Should().Be(6);
        CountColor(point, MeshOverlay.HoverColor).Should().Be(36);
    }

    private int FacingFace => Enumerable.Range(0, _box.Faces.Count).First(face => MeshPicker.IsFacingCamera(_box, Matrix4x4.Identity, _camera, face));

    private SolidEdge FacingEdge => new SolidTopology(_box).GetFaceEdges(FacingFace).First();

    private int FacingPoint => _box.Faces[FacingFace][0];

    private OverlayBuilder Draw(MeshHit? hover)
    {
        OverlayBuilder overlay = new();
        overlay.Begin(_camera, MeshTestsBuilder.Height);
        MeshOverlay.Draw(overlay, _camera, _box, Matrix4x4.Identity, _selection, hover);
        return overlay;
    }

    private static int CountColor(OverlayBuilder overlay, Vector4 color) => overlay.Vertices.ToArray().Count(vertex => vertex.Color == color);
}
