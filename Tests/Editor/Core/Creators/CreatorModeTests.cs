using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Core.Tests.Creators;

public sealed class CreatorModeTests : IDisposable
{
    private const int Width = CreatorModeTestsBuilder.Width;
    private const int Height = CreatorModeTestsBuilder.Height;

    private readonly CreatorModeTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void EnteringInPlaceGreysEverythingButTheCreation()
    {
        Entity van = _builder.Find("Van");
        Entity other = _builder.Find("Van 2");
        Entity crate = _builder.Find("Crate");

        using CreatorMode mode = _builder.EnterInPlace("Van");

        World world = _builder.World;
        mode.Root.Should().Be(van);
        mode.Placement.Should().Be(CreatorPlacement.InPlace);
        world.Has<Greyed>(crate).Should().BeTrue();
        _builder.Parts.FindAll(other).Should().OnlyContain(part => world.Has<Greyed>(part));
        _builder.Parts.FindAll(van).Should().OnlyContain(part => !world.Has<Greyed>(part) && !world.Has<Undrawn>(part));
        world.Has<Undrawn>(crate).Should().BeFalse();
    }

    [Fact]
    public void EnteringAloneSetsTheLevelAsideAroundATemporaryCopyThatIsNeverSaved()
    {
        Entity crate = _builder.Find("Crate");
        Entity other = _builder.Find("Van 2");

        using CreatorMode mode = _builder.EnterAlone();

        World world = _builder.World;
        mode.Placement.Should().Be(CreatorPlacement.Alone);
        world.Get<Vehicle>(mode.Root).File.Should().Be(CreatorModeTestsBuilder.File);
        _builder.Parts.FindAll(mode.Root).Should().HaveCount(5).And.OnlyContain(part => !world.Has<Undrawn>(part));
        world.Has<Undrawn>(crate).Should().BeTrue();
        _builder.Parts.FindAll(other).Should().OnlyContain(part => world.Has<Undrawn>(part));
        _builder.Scene.Entities.Should().NotContain(mode.Root).And.HaveCount(3);
        _builder.Scene.IsUnsaved.Should().BeFalse();
    }

    [Fact]
    public void PicksOnlyTheCreationsParts()
    {
        using CreatorMode mode = _builder.EnterInPlace("Van");
        Entity van = _builder.Find("Van");
        Entity wheel = _builder.Parts.Find(van, "wheel.1L");
        Vector3 wheelCenter = _builder.World.Get<Transform>(wheel).Position;
        Vector2 onWheel = _builder.PixelOf(wheelCenter);
        Vector2 onCrate = _builder.PixelOf(new Vector3(-4, 0.25f, 0));
        Ray toCrate = _builder.Camera.GetRay(onCrate, Width, Height);
        Entity? crate = _builder.Scene.Picker.Pick(_builder.World, toCrate);
        _builder.Scene.Selection.Select(van);

        mode.PointerPressed(onWheel, Width, Height);
        string? picked = mode.SelectedPart;
        mode.PointerPressed(onCrate, Width, Height);

        crate.Should().Be(_builder.Find("Crate"));
        picked.Should().Be("wheel.1L");
        mode.SelectedPart.Should().BeNull();
        _builder.Scene.Selection.Selected.Should().Be(van);
    }

    [Fact]
    public void SelectsAPartByItsIdAsThePartsListDoes()
    {
        using CreatorMode mode = _builder.EnterInPlace("Van");
        Entity van = _builder.Find("Van");
        int raised = 0;
        mode.PartSelected += () => raised++;

        mode.SelectPart("wheel.2R");
        mode.SelectPart("wheel.2R");
        Entity? selected = mode.FindSelectedPart();
        mode.SelectPart(null);

        selected.Should().Be(_builder.Parts.Find(van, "wheel.2R"));
        raised.Should().Be(2, "selecting the same part again changes nothing");
        mode.SelectedPart.Should().BeNull();
        mode.FindSelectedPart().Should().BeNull();
    }

    [Fact]
    public void LeavingClearsTheMarksTheCopyAndTheWorkLight()
    {
        CreatorMode inPlace = _builder.EnterInPlace("Van");
        Entity inPlaceLight = inPlace.WorkLight;
        inPlace.Dispose();
        bool inPlaceLightGone = !_builder.World.IsAlive(inPlaceLight);
        int greyedAfterInPlace = CountWith<Greyed>(_builder.World);
        CreatorMode alone = _builder.EnterAlone();
        Entity copy = alone.Root;
        IReadOnlyList<Entity> copyParts = _builder.Parts.FindAll(copy);
        Entity light = alone.WorkLight;

        alone.Dispose();

        World world = _builder.World;
        CountWith<Greyed>(world).Should().Be(0);
        CountWith<Undrawn>(world).Should().Be(0);
        world.IsAlive(copy).Should().BeFalse();
        copyParts.Should().OnlyContain(part => !world.IsAlive(part));
        world.IsAlive(light).Should().BeFalse();
        inPlaceLightGone.Should().BeTrue();
        greyedAfterInPlace.Should().Be(0);
        world.IsAlive(_builder.Find("Van")).Should().BeTrue();
    }

    [Fact]
    public void KeepsAWorkLightAtTheView()
    {
        using CreatorMode mode = _builder.EnterInPlace("Van");
        _builder.Camera.LookAt(new Vector3(1, 0, 2), yaw: -1.2f, pitch: -0.3f, distance: 6);

        mode.Update();

        World world = _builder.World;
        world.Has<PointLight>(mode.WorkLight).Should().BeTrue();
        Vector3 position = world.Get<Transform>(mode.WorkLight).Position;
        Vector3.Distance(position, _builder.Camera.Position).Should().BeLessThan(1e-4f);
        world.Get<PointLight>(mode.WorkLight).Range.Should().BeGreaterThan(_builder.Camera.PivotDistance);
        _builder.Scene.Entities.Should().NotContain(mode.WorkLight);
    }

    [Fact]
    public void UndoesAndSavesTheCreationsFile()
    {
        using CreatorMode mode = _builder.EnterInPlace("Van");

        bool saved = mode.Save();

        saved.Should().BeTrue();
        _builder.Saves.Should().Be(1);
        mode.History.Should().BeSameAs(_builder.FileHistory);
        mode.History.Should().NotBeSameAs(_builder.Scene.History);
    }

    [Fact]
    public void GreysThePartsTheFileGivesTheOtherCopiesWhileOpen()
    {
        using CreatorMode mode = _builder.EnterInPlace("Van");
        Entity van = _builder.Find("Van");
        Entity other = _builder.Find("Van 2");

        _builder.WriteWheels(6);

        World world = _builder.World;
        _builder.Parts.FindAll(other).Should().HaveCount(7).And.OnlyContain(part => world.Has<Greyed>(part));
        _builder.Parts.FindAll(van).Should().HaveCount(7).And.OnlyContain(part => !world.Has<Greyed>(part));
    }

    private static int CountWith<T>(World world)
    {
        QueryDescription query = new QueryDescription().WithAll<T>();
        return world.CountEntities(in query);
    }

    [Fact]
    public void ThereIsNoMeshToEnterUntilAPartIsSelected()
    {
        using CreatorMode mode = _builder.EnterAlone();

        bool entered = mode.EnterMesh();

        entered.Should().BeFalse();
        mode.MeshSelection.Should().BeNull();
    }

    [Fact]
    public void EnteringTheMeshOfTheSelectedPartGivesAnEmptySelectionInPointMode()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        int changes = 0;
        mode.MeshModeChanged += () => changes++;

        bool entered = mode.EnterMesh();

        entered.Should().BeTrue();
        mode.MeshPart.Should().Be("body");
        mode.MeshSelection!.Element.Should().Be(FlickerVoid.Editor.Core.Meshes.MeshElement.Point);
        mode.MeshSelection.Points.Should().BeEmpty();
        changes.Should().Be(1);
    }

    [Fact]
    public void WhileTheMeshIsEnteredAClickSelectsInTheMeshAndNotAnotherPart()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        mode.MeshSelection!.SetElement(FlickerVoid.Editor.Core.Meshes.MeshElement.Face);
        Vector2 onBody = _builder.PixelOf(new Vector3(0, 1.5f, 0));

        mode.PointerPressed(onBody, Width, Height);

        mode.SelectedPart.Should().Be("body");
        mode.MeshSelection.Faces.Should().ContainSingle();
    }

    [Fact]
    public void ShiftAddsAndAltSelectsALoopInTheMesh()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        mode.MeshSelection!.SetElement(FlickerVoid.Editor.Core.Meshes.MeshElement.Face);
        Vector2 top = _builder.PixelOf(new Vector3(0, 1.65f, 0));
        Vector2 side = _builder.PixelOf(new Vector3(0.9f, 1, 0));

        mode.PressMesh(top, Width, Height, additive: false, loop: false);
        mode.PressMesh(side, Width, Height, additive: true, loop: false);

        mode.MeshSelection.Faces.Should().HaveCount(2);
    }

    [Fact]
    public void TheMouseOverAnElementOfTheEnteredMeshHoversItAndLeavingTheMeshClearsIt()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        mode.MeshSelection!.SetElement(FlickerVoid.Editor.Core.Meshes.MeshElement.Face);
        Vector2 top = _builder.PixelOf(new Vector3(0, 1.65f, 0));

        mode.PointerMoved(top, Width, Height);
        FlickerVoid.Editor.Core.Meshes.MeshHit? hovered = mode.Hover;
        mode.LeaveMesh();

        hovered.Should().NotBeNull();
        mode.Hover.Should().BeNull();
    }

    [Fact]
    public void TheMeshIsDrawnOnlyWhileItIsEntered()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        FlickerVoid.Editor.Core.Overlays.OverlayBuilder outside = new();
        outside.Begin(_builder.Camera, Height);
        FlickerVoid.Editor.Core.Overlays.OverlayBuilder inside = new();
        inside.Begin(_builder.Camera, Height);

        mode.DrawMesh(outside);
        mode.EnterMesh();
        mode.DrawMesh(inside);

        outside.Vertices.Length.Should().Be(0);
        inside.Vertices.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MovingTheSelectedPointsEditsThePartsMeshAndKeepsTheSelection()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        mode.MeshSelection!.Select(FlickerVoid.Editor.Core.Meshes.MeshHit.OfPoint(0), additive: false);
        Vector3 before = _builder.FindMesh("body")!.Points[0];

        bool moved = mode.MoveMesh(new Vector3(0, 0.25f, 0));

        moved.Should().BeTrue();
        _builder.MeshEdits.Should().Equal("body");
        _builder.FindMesh("body")!.Points[0].Y.Should().BeApproximately(before.Y + 0.25f, 1e-5f);
        mode.MeshSelection.Points.Should().Equal(0);
    }

    [Fact]
    public void RotatingAndScalingNeedASelectionAndOneEditEach()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();

        bool withNothing = mode.ScaleMesh(new Vector3(2, 2, 2));
        mode.MeshSelection!.Select(FlickerVoid.Editor.Core.Meshes.MeshHit.OfPoint(0), additive: false);
        mode.MeshSelection.Select(FlickerVoid.Editor.Core.Meshes.MeshHit.OfPoint(1), additive: true);
        bool rotated = mode.RotateMesh(new Vector3(0, 0, 45));
        bool scaled = mode.ScaleMesh(new Vector3(2, 1, 1));

        withNothing.Should().BeFalse();
        rotated.Should().BeTrue();
        scaled.Should().BeTrue();
        _builder.MeshEdits.Should().HaveCount(2);
    }

    [Fact]
    public void ExtrudingTheSelectedFaceEditsTheMeshOnceAndSelectsTheNewFace()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        mode.MeshSelection!.SetElement(FlickerVoid.Editor.Core.Meshes.MeshElement.Face);
        mode.MeshSelection.Select(FlickerVoid.Editor.Core.Meshes.MeshHit.OfFace(2, default), additive: false);
        int points = _builder.FindMesh("body")!.Points.Count;

        bool extruded = mode.ExtrudeMesh(0.5f);

        extruded.Should().BeTrue();
        _builder.MeshEdits.Should().Equal("body");
        _builder.FindMesh("body")!.Points.Should().HaveCount(points + 4);
        mode.MeshSelection!.Faces.Should().ContainSingle();
    }

    [Fact]
    public void InsettingTheSelectedFaceEditsTheMeshOnceAndSelectsTheInnerFace()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        mode.MeshSelection!.SetElement(FlickerVoid.Editor.Core.Meshes.MeshElement.Face);
        mode.MeshSelection.Select(FlickerVoid.Editor.Core.Meshes.MeshHit.OfFace(2, default), additive: false);
        int faces = _builder.FindMesh("body")!.Faces.Count;

        bool inset = mode.InsetMesh(0.1f);

        inset.Should().BeTrue();
        _builder.MeshEdits.Should().Equal("body");
        _builder.FindMesh("body")!.Faces.Should().HaveCount(faces + 4);
        mode.MeshSelection!.Faces.Should().ContainSingle();
    }

    [Fact]
    public void ALoopCutAtTheFaceUnderTheMouseEditsTheMeshOnceAndSelectsTheNewEdges()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        Vector2 top = _builder.PixelOf(new Vector3(0, 1.65f, 0));
        int faces = _builder.FindMesh("body")!.Faces.Count;

        bool cut = mode.LoopCutMesh(top, Width, Height, 1, 0);

        cut.Should().BeTrue();
        _builder.MeshEdits.Should().Equal("body");
        _builder.FindMesh("body")!.Faces.Count.Should().BeGreaterThan(faces);
        mode.MeshSelection!.Element.Should().Be(FlickerVoid.Editor.Core.Meshes.MeshElement.Edge);
        mode.MeshSelection.Edges.Should().NotBeEmpty();
    }

    [Fact]
    public void ALoopCutBesideTheSolidDoesNothing()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();

        bool cut = mode.LoopCutMesh(new Vector2(5, 5), Width, Height, 1, 0);

        cut.Should().BeFalse();
        _builder.MeshEdits.Should().BeEmpty();
    }

    [Fact]
    public void BevelingTheSelectedEdgeEditsTheMeshOnceAndSelectsTheStrip()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        mode.MeshSelection!.SetElement(FlickerVoid.Editor.Core.Meshes.MeshElement.Edge);
        mode.MeshSelection.Select(FlickerVoid.Editor.Core.Meshes.MeshHit.OfEdge(FlickerVoid.Engine.Runtime.Solids.SolidEdge.Of(0, 1)), additive: false);
        int faces = _builder.FindMesh("body")!.Faces.Count;

        bool beveled = mode.BevelMesh(0.1f, 2, round: true);

        beveled.Should().BeTrue();
        _builder.MeshEdits.Should().Equal("body");
        _builder.FindMesh("body")!.Faces.Should().HaveCount(faces + 2);
        mode.MeshSelection!.Faces.Should().HaveCount(2);
    }

    [Fact]
    public void TheMirrorSwitchIsKeptInTheFileAndNeedsTheMeshEntered()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");

        bool outside = mode.SetMirror(true);
        mode.EnterMesh();
        bool inside = mode.SetMirror(true);

        outside.Should().BeFalse();
        inside.Should().BeTrue();
        mode.Mirror.Should().BeTrue();
        _builder.IsMirrored("body").Should().BeTrue();
    }

    [Fact]
    public void WithTheMirrorOnMovingAPointMovesItsTwinTheOtherWay()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        mode.SetMirror(true);
        mode.MeshSelection!.Select(FlickerVoid.Editor.Core.Meshes.MeshHit.OfPoint(1), additive: false);

        mode.MoveMesh(new Vector3(0.2f, 0, 0));

        FlickerVoid.Engine.Runtime.Solids.SolidMesh body = _builder.FindMesh("body")!;
        body.Points[0].X.Should().BeApproximately(-body.Points[1].X, 1e-4f);
    }

    [Fact]
    public void WithTheMirrorOnExtrudingASideFaceExtrudesTheOtherSideToo()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        mode.SetMirror(true);
        mode.MeshSelection!.SetElement(FlickerVoid.Editor.Core.Meshes.MeshElement.Face);
        mode.MeshSelection.Select(FlickerVoid.Editor.Core.Meshes.MeshHit.OfFace(0, default), additive: false);
        int points = _builder.FindMesh("body")!.Points.Count;

        mode.ExtrudeMesh(0.3f);

        _builder.FindMesh("body")!.Points.Should().HaveCount(points + 8);
        mode.MeshSelection!.Faces.Should().HaveCount(2);
    }

    [Fact]
    public void WithTheMirrorOnALoopCutLeavesTheSolidSymmetric()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        mode.SetMirror(true);
        Vector2 side = _builder.PixelOf(new Vector3(0.9f, 1, 0));

        bool cut = mode.LoopCutMesh(side, Width, Height, 1, 0.3f);

        cut.Should().BeTrue();
        FlickerVoid.Editor.Core.Meshes.MeshMirror.FindTwins(_builder.FindMesh("body")!).Should().NotContain(-1);
    }

    [Fact]
    public void ExtrudingWithNoFaceSelectedDoesNothing()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();

        bool extruded = mode.ExtrudeMesh(0.5f);

        extruded.Should().BeFalse();
        _builder.MeshEdits.Should().BeEmpty();
    }

    [Fact]
    public void ASelectionOutsideTheMeshModeEditsNothing()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");

        bool moved = mode.MoveMesh(Vector3.One);

        moved.Should().BeFalse();
        _builder.MeshEdits.Should().BeEmpty();
    }

    [Fact]
    public void LeavingTheMeshDropsTheSelectionAndClicksSelectPartsAgain()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();

        mode.LeaveMesh();
        mode.PointerPressed(_builder.PixelOf(new Vector3(0.8f, 0.35f, 1.35f)), Width, Height);

        mode.MeshSelection.Should().BeNull();
        mode.MeshPart.Should().BeNull();
        mode.SelectedPart.Should().Be("wheel.1L");
    }

    [Fact]
    public void SelectingAnotherPartLeavesTheMesh()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();

        mode.SelectPart("wheel.1L");

        mode.MeshSelection.Should().BeNull();
    }

    [Fact]
    public void ANewFileWithTheSameFacesKeepsTheMeshEnteredAndItsSelection()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        mode.MeshSelection!.SetElement(FlickerVoid.Editor.Core.Meshes.MeshElement.Face);
        mode.MeshSelection.SelectAll();

        _builder.ReplaceCar();

        mode.MeshSelection.Should().NotBeNull();
        mode.MeshSelection!.Element.Should().Be(FlickerVoid.Editor.Core.Meshes.MeshElement.Face);
        mode.MeshSelection.Faces.Should().NotBeEmpty();
    }
}
