using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Greyboxes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Creators;

public sealed class CreatorModePieceTests : IDisposable
{
    private readonly CreatorModeTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShapingAGreyboxGreysTheRestOfTheLevelAndSelectsThePiece()
    {
        Entity crate = _builder.Find("Crate");

        using CreatorMode mode = _builder.EnterPiece("Crate");

        mode.Placement.Should().Be(CreatorPlacement.Piece);
        mode.Root.Should().Be(crate);
        mode.SelectedPart.Should().Be(CreatorMode.PieceId);
        mode.FindSelectedPart().Should().Be(crate);
        mode.History.Should().BeSameAs(_builder.Scene.History, "the piece belongs to the level");
        _builder.World.Has<Greyed>(crate).Should().BeFalse();
        _builder.World.Has<Greyed>(_builder.Find("Van")).Should().BeTrue();
    }

    [Fact]
    public void ThePiecesMeshIsTheBoxOfItsSizeUntilItIsShaped()
    {
        using CreatorMode mode = _builder.EnterPiece("Crate");

        bool entered = mode.EnterMesh();

        entered.Should().BeTrue();
        mode.MeshSelection!.Should().NotBeNull();
        _builder.World.Get<Greybox>(_builder.Find("Crate")).Mesh.Should().BeNull("entering changes nothing");
    }

    [Fact]
    public void AMeshEditGivesTheGreyboxItsShapeAsOneUndoStepOfTheLevel()
    {
        Entity crate = _builder.Find("Crate");
        using CreatorMode mode = _builder.EnterPiece("Crate");
        mode.EnterMesh();
        mode.MeshSelection!.SetElement(MeshElement.Face);
        mode.MeshSelection.SelectFaces([TopFace()]);

        bool extruded = mode.ExtrudeMesh(0.5f);

        extruded.Should().BeTrue();
        SolidMesh? shape = _builder.World.Get<Greybox>(crate).Mesh;
        shape.Should().NotBeNull();
        shape!.Points.Max(point => point.Y).Should().BeApproximately(1.5f, 1e-4f);
        mode.MeshSelection.Faces.Should().ContainSingle("the selection follows the new solid");
        _builder.Scene.History.Undo();
        _builder.World.Get<Greybox>(crate).Mesh.Should().BeNull("undo puts the box back");
        mode.MeshSelection.Should().NotBeNull("the mesh stays entered");
    }

    [Fact]
    public void AShapedPieceIsPickedByItsShape()
    {
        Entity crate = _builder.Find("Crate");
        using CreatorMode mode = _builder.EnterPiece("Crate");
        mode.EnterMesh();
        mode.MeshSelection!.SetElement(MeshElement.Face);
        mode.MeshSelection.SelectFaces([TopFace()]);
        mode.ExtrudeMesh(2);
        Ray above = new(new Vector3(-4 + 10, 2.5f, 0), -Vector3.UnitX);

        Entity? picked = _builder.Scene.Picker.Pick(_builder.World, above);

        picked.Should().Be(crate, "the ray passes over the box and hits what was extruded");
    }

    [Fact]
    public void ClicksNeverDeselectThePieceAndLeavingUngreysTheLevel()
    {
        CreatorMode mode = _builder.EnterPiece("Crate");

        mode.PointerPressed(new Vector2(5, 5), CreatorModeTestsBuilder.Width, CreatorModeTestsBuilder.Height);
        mode.SelectPart(null);
        string? selected = mode.SelectedPart;
        mode.Dispose();

        selected.Should().Be(CreatorMode.PieceId);
        _builder.World.Has<Greyed>(_builder.Find("Van")).Should().BeFalse();
        _builder.World.IsAlive(_builder.Find("Crate")).Should().BeTrue("the piece stays in the level");
    }

    private static int TopFace()
    {
        SolidMesh box = SolidMesh.Box(Vector3.One);
        for (int face = 0; face < box.Faces.Count; face++)
        {
            if (box.Faces[face].All(point => box.Points[point].Y > 0.5f))
            {
                return face;
            }
        }

        throw new InvalidOperationException("A box has a top.");
    }
}
