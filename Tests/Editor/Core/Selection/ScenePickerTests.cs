using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Greyboxes;
using FlickerVoid.Editor.Core.Selection;

namespace FlickerVoid.Editor.Core.Tests.Selection;

public sealed class ScenePickerTests : IDisposable
{
    private static readonly Ray DownTheZAxis = new(new Vector3(0, 0, 10), -Vector3.UnitZ);

    private readonly ScenePickerTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void PicksTheMeshUnderTheRay()
    {
        _builder.WithSquare("Square.glb", 1);
        Entity square = _builder.AddMesh("Square.glb", Vector3.Zero);
        ScenePicker picker = _builder.Build();

        Entity? picked = picker.Pick(_builder.World, DownTheZAxis);

        picked.Should().Be(square);
    }

    [Fact]
    public void PicksGreyboxesByTheirBox()
    {
        Greybox greybox = new() { Size = new Vector3(2, 2, 2) };
        Entity box = _builder.AddGreybox(greybox, new Vector3(0, -1, 0));
        ScenePicker picker = _builder.Build();

        Entity? picked = picker.Pick(_builder.World, DownTheZAxis, out float distance);

        picked.Should().Be(box);
        distance.Should().BeApproximately(9, 0.001f);
    }

    [Fact]
    public void TellsHowFarAlongTheRayTheMeshIsHit()
    {
        _builder.WithSquare("Square.glb", 1);
        _builder.AddMesh("Square.glb", new Vector3(0, 0, 2));
        ScenePicker picker = _builder.Build();

        picker.Pick(_builder.World, DownTheZAxis, out float distance);

        distance.Should().BeApproximately(8, 0.001f);
    }

    [Fact]
    public void PicksNothingWhereTheRayMissesEveryMesh()
    {
        _builder.WithSquare("Square.glb", 1);
        _builder.AddMesh("Square.glb", new Vector3(5, 0, 0));
        ScenePicker picker = _builder.Build();

        Entity? picked = picker.Pick(_builder.World, DownTheZAxis);

        picked.Should().BeNull();
    }

    [Fact]
    public void PicksTheNearestOfOverlappingMeshes()
    {
        _builder.WithSquare("Square.glb", 1);
        _builder.AddMesh("Square.glb", new Vector3(0, 0, -2));
        Entity near = _builder.AddMesh("Square.glb", new Vector3(0, 0, 2));
        ScenePicker picker = _builder.Build();

        Entity? picked = picker.Pick(_builder.World, DownTheZAxis);

        picked.Should().Be(near);
    }

    [Fact]
    public void PicksMeshesByTheirScaledShape()
    {
        _builder.WithSquare("Square.glb", 1);
        Entity scaled = _builder.AddMesh("Square.glb", new Vector3(2.5f, 0, 0), scale: 3);
        ScenePicker picker = _builder.Build();

        Entity? picked = picker.Pick(_builder.World, DownTheZAxis);

        picked.Should().Be(scaled);
    }

    [Fact]
    public void SkipsMeshesWhoseModelIsMissing()
    {
        _builder.AddMesh("Missing.glb", Vector3.Zero);
        ScenePicker picker = _builder.Build();

        Entity? picked = picker.Pick(_builder.World, DownTheZAxis);

        picked.Should().BeNull();
    }

    [Fact]
    public void ReloadsAModelWhenItsFileChanges()
    {
        _builder.WithSquare("Square.glb", 0.1f);
        Entity square = _builder.AddMesh("Square.glb", new Vector3(0.5f, 0, 0));
        ScenePicker picker = _builder.Build();
        Entity? before = picker.Pick(_builder.World, DownTheZAxis);

        _builder.WithSquare("Square.glb", 1);
        _builder.NotifyChanged("Square.fvmodel");
        Entity? after = picker.Pick(_builder.World, DownTheZAxis);

        before.Should().BeNull();
        after.Should().Be(square);
    }

    [Fact]
    public void PicksTheMeshesAModuleMakes()
    {
        _builder.WithFile("Van.fvtest", "2").WithMeshSource(new TestMeshSource());
        Entity van = _builder.AddMesh("Van.fvtest#body", new Vector3(0, -1, 0));
        ScenePicker picker = _builder.Build();

        Entity? picked = picker.Pick(_builder.World, DownTheZAxis, out float distance);

        picked.Should().Be(van);
        distance.Should().BeApproximately(9, 0.001f);
    }

    [Fact]
    public void MakesAMeshAgainWhenItsFileChanges()
    {
        _builder.WithFile("Van.fvtest", "2").WithMeshSource(new TestMeshSource());
        Entity van = _builder.AddMesh("Van.fvtest#body", new Vector3(0, -1, 0));
        ScenePicker picker = _builder.Build();
        Entity? before = picker.Pick(_builder.World, DownTheZAxis);

        _builder.WithFile("Van.fvtest", "0.5");
        _builder.NotifyChanged("Van.fvtest");
        Entity? after = picker.Pick(_builder.World, DownTheZAxis);

        before.Should().Be(van);
        after.Should().BeNull("a box half a metre high stands below the ray");
    }

    [Fact]
    public void PicksAModelBuiltAfterItWasMissing()
    {
        Entity square = _builder.AddMesh("Square.glb", Vector3.Zero);
        ScenePicker picker = _builder.Build();
        Entity? before = picker.Pick(_builder.World, DownTheZAxis);

        _builder.WithSquare("Square.glb", 1);
        _builder.NotifyChanged("Square.fvmodel");
        Entity? after = picker.Pick(_builder.World, DownTheZAxis);

        before.Should().BeNull();
        after.Should().Be(square);
    }

    [Fact]
    public void GivesTheLocalBoundsOfAPickedMesh()
    {
        _builder.WithSquare("Square.glb", 1);
        Entity square = _builder.AddMesh("Square.glb", Vector3.Zero);
        ScenePicker picker = _builder.Build();

        bool found = picker.TryGetBounds(_builder.World, square, out Vector3 min, out Vector3 max);

        found.Should().BeTrue();
        min.Should().Be(new Vector3(-1, -1, 0));
        max.Should().Be(new Vector3(1, 1, 0));
    }
}
