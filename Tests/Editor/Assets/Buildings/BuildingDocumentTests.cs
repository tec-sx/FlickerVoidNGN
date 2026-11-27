using FlickerVoid.Editor.Assets.Buildings;
using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Assets.Tests.Buildings;

public sealed class BuildingDocumentTests : IDisposable
{
    private readonly BuildingDocumentTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void HoldsStoreysUnsavedWhereEveryLoadSeesThem()
    {
        BuildingDocument document = _builder.Build();

        document.SetStoreys(3, true);

        _builder.Content.IsUnsaved(BuildingDocumentTestsBuilder.House).Should().BeTrue();
        _builder.Load().Storeys.Should().HaveCount(4, "placed copies load what is held");
        _builder.ReadDisk().Storeys.Should().ContainSingle("nothing is saved yet");
    }

    [Fact]
    public void AddsStairsAsOneUndoStep()
    {
        BuildingDocument document = _builder.Build();
        document.SetStoreys(2, false);

        string? id = document.AddStairs(0);

        id.Should().Be("stairs.1");
        _builder.Load().Stairs.Should().ContainSingle();
        UndoHistory history = _builder.Content.GetHistory(BuildingDocumentTestsBuilder.House);
        history.Undo();
        _builder.Load().Stairs.Should().BeEmpty();
        _builder.Load().Storeys.Should().HaveCount(2);
    }

    [Fact]
    public void HoldsNothingForStairsWithoutAStoreyAbove()
    {
        BuildingDocument document = _builder.Build();

        string? id = document.AddStairs(0);

        id.Should().BeNull();
        _builder.Content.IsUnsaved(BuildingDocumentTestsBuilder.House).Should().BeFalse();
    }

    [Fact]
    public void AddsAndRemovesAnElevator()
    {
        BuildingDocument document = _builder.Build();
        document.SetStoreys(2, false);

        document.AddElevator();
        bool added = _builder.Load().Elevator is not null;
        document.RemoveElevator();

        added.Should().BeTrue();
        _builder.Load().Elevator.Should().BeNull();
    }

    [Fact]
    public void GivesAPartANewSolidLikeEveryCreation()
    {
        BuildingDocument document = _builder.Build();
        FlickerVoid.Engine.Runtime.Solids.SolidMesh box = FlickerVoid.Engine.Runtime.Solids.SolidMesh.Box(new System.Numerics.Vector3(1, 1, 1));

        document.SetMesh("roof", box);

        _builder.Load().Roof.Mesh.Faces.Should().HaveCount(6);
    }
}
