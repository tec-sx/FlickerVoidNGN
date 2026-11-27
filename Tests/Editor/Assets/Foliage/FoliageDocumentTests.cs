using System.Numerics;

using FlickerVoid.Editor.Assets.Foliage;
using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Foliage;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Assets.Tests.Foliage;

public sealed class FoliageDocumentTests : IDisposable
{
    private readonly FoliageDocumentTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void HoldsAnotherSeedsPlantUnsavedWhereEveryLoadSeesIt()
    {
        FoliageDocument document = _builder.Build();
        Vector3 limb = _builder.Load().Limbs[0].Place.Position;

        document.SetSeed(8);

        _builder.Content.IsUnsaved(FoliageDocumentTestsBuilder.Oak).Should().BeTrue();
        _builder.Load().Seed.Should().Be(8, "placed copies load what is held");
        _builder.Load().Limbs[0].Place.Position.Should().NotBe(limb);
        _builder.ReadDisk().Seed.Should().Be(1, "nothing is saved yet");
    }

    [Fact]
    public void TurnsTheTreeIntoABushAsOneUndoStep()
    {
        FoliageDocument document = _builder.Build();

        document.SetKind(FoliageKind.Bush);

        _builder.Load().Trunk.Should().BeNull();
        UndoHistory history = _builder.Content.GetHistory(FoliageDocumentTestsBuilder.Oak);
        history.Undo();
        _builder.Load().Kind.Should().Be(FoliageKind.Tree);
        _builder.Load().Trunk.Should().NotBeNull();
    }

    [Fact]
    public void GrowsThePlantFromANewShape()
    {
        FoliageDocument document = _builder.Build();
        FoliageShape shape = document.Plant.Shape.Clone();
        shape.Limbs = 2;

        bool grown = document.SetShape(shape);

        grown.Should().BeTrue();
        _builder.Load().Limbs.Should().HaveCount(2);
    }

    [Fact]
    public void HoldsNothingForAShapeNothingGrowsFrom()
    {
        FoliageDocument document = _builder.Build();
        FoliageShape shape = document.Plant.Shape.Clone();
        shape.Height = 0;

        bool grown = document.SetShape(shape);

        grown.Should().BeFalse();
        document.Plant.Shape.Height.Should().Be(6);
        _builder.Content.IsUnsaved(FoliageDocumentTestsBuilder.Oak).Should().BeFalse();
    }

    [Fact]
    public void KeepsAMeshToolsWorkOnAPartUntilThePlantIsGrownAgain()
    {
        FoliageDocument document = _builder.Build();
        SolidMesh box = SolidMesh.Box(Vector3.One);

        document.SetMesh("crown.1", box);

        _builder.Load().Crowns[0].Mesh.Points.Should().HaveCount(8);
        document.SetSeed(2);
        _builder.Load().Crowns[0].Mesh.Points.Should().HaveCount(12);
    }

    [Fact]
    public void NoPartOfAPlantIsAWallForADoor()
    {
        FoliageDocument document = _builder.Build();

        bool wall = document.IsDoorWall("trunk");

        wall.Should().BeFalse();
        _builder.Content.IsUnsaved(FoliageDocumentTestsBuilder.Oak).Should().BeFalse();
    }
}
