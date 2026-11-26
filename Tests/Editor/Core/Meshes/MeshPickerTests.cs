using System.Numerics;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class MeshPickerTests
{
    private const int Width = MeshTestsBuilder.Width;
    private const int Height = MeshTestsBuilder.Height;

    private readonly SolidMesh _box = MeshTestsBuilder.Box();

    [Fact]
    public void AClickOnAFaceFindsItAndTheEdgeNearestTheMouse()
    {
        EditorCamera camera = MeshTestsBuilder.FrontCamera();
        MeshPicker picker = new(_box, Matrix4x4.Identity, camera, Width, Height);
        Vector2 top = camera.Project(new Vector3(0, 0.9f, 0.5f), Width, Height);

        MeshHit? hit = picker.Pick(MeshElement.Face, top);

        hit.Should().NotBeNull();
        hit!.Value.Element.Should().Be(MeshElement.Face);
        hit.Value.Index.Should().Be(4);
        hit.Value.Edge.Should().Be(SolidEdge.Of(6, 7));
    }

    [Fact]
    public void AClickBesideTheSolidFindsNothing()
    {
        MeshPicker picker = new(_box, Matrix4x4.Identity, MeshTestsBuilder.FrontCamera(), Width, Height);

        MeshHit? hit = picker.Pick(MeshElement.Face, new Vector2(20, 20));

        hit.Should().BeNull();
    }

    [Fact]
    public void AClickNearAnEdgeFindsIt()
    {
        EditorCamera camera = MeshTestsBuilder.FrontCamera();
        MeshPicker picker = new(_box, Matrix4x4.Identity, camera, Width, Height);
        Vector2 onEdge = camera.Project(new Vector3(0, 1, 0.5f), Width, Height);

        MeshHit? hit = picker.Pick(MeshElement.Edge, onEdge + new Vector2(0, 3));

        hit.Should().NotBeNull();
        hit!.Value.Edge.Should().Be(SolidEdge.Of(6, 7));
    }

    [Fact]
    public void AClickFarFromEveryEdgeFindsNone()
    {
        EditorCamera camera = MeshTestsBuilder.FrontCamera();
        MeshPicker picker = new(_box, Matrix4x4.Identity, camera, Width, Height);
        Vector2 centre = camera.Project(new Vector3(0, 0.5f, 0.5f), Width, Height);

        MeshHit? hit = picker.Pick(MeshElement.Edge, centre);

        hit.Should().BeNull();
    }

    [Fact]
    public void AClickNearAPointFindsIt()
    {
        EditorCamera camera = MeshTestsBuilder.FrontCamera();
        MeshPicker picker = new(_box, Matrix4x4.Identity, camera, Width, Height);
        Vector2 corner = camera.Project(new Vector3(0.5f, 1, 0.5f), Width, Height);

        MeshHit? hit = picker.Pick(MeshElement.Point, corner + new Vector2(2, -2));

        hit.Should().NotBeNull();
        hit!.Value.Index.Should().Be(7);
    }

    [Fact]
    public void OnlyWhatFacesTheCameraCanBePicked()
    {
        EditorCamera camera = MeshTestsBuilder.CornerCamera();
        MeshPicker picker = new(_box, Matrix4x4.Identity, camera, Width, Height);
        int hidden = FindHiddenPoint(camera);
        Vector2 where = camera.Project(_box.Points[hidden], Width, Height);

        MeshHit? point = picker.Pick(MeshElement.Point, where);
        MeshHit? edge = picker.Pick(MeshElement.Edge, where);

        point?.Index.Should().NotBe(hidden);
        if (edge is { } found)
        {
            found.Edge.A.Should().NotBe(hidden);
            found.Edge.B.Should().NotBe(hidden);
        }
    }

    [Fact]
    public void ThePartsPlaceMovesWhatIsPicked()
    {
        EditorCamera camera = MeshTestsBuilder.FrontCamera();
        Matrix4x4 moved = Matrix4x4.CreateTranslation(3, 0, 0);
        MeshPicker picker = new(_box, moved, camera, Width, Height);
        Vector2 onMoved = camera.Project(new Vector3(3, 0.5f, 0.5f), Width, Height);
        Vector2 onOrigin = camera.Project(new Vector3(0, 0.5f, 0.5f), Width, Height);

        MeshHit? hit = picker.Pick(MeshElement.Face, onMoved);
        MeshHit? miss = picker.Pick(MeshElement.Face, onOrigin);

        hit.Should().NotBeNull();
        miss.Should().BeNull();
    }

    /// <summary>The corner no face turned to the camera touches.</summary>
    private int FindHiddenPoint(EditorCamera camera)
    {
        for (int point = 0; point < _box.Points.Count; point++)
        {
            bool shown = false;
            for (int face = 0; face < _box.Faces.Count; face++)
            {
                if (_box.Faces[face].Contains(point) && MeshPicker.IsFacingCamera(_box, Matrix4x4.Identity, camera, face))
                {
                    shown = true;
                }
            }

            if (!shown)
            {
                return point;
            }
        }

        throw new InvalidOperationException("Every corner shows.");
    }
}
