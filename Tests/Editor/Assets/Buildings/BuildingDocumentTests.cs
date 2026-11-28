using FlickerVoid.Editor.Assets.Buildings;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Runtime.Solids;

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
    public void MakesThePartsAgainAfterAChangeAsOneUndoStep()
    {
        BuildingDocument document = _builder.Build();

        document.Change(static building => building.Storeys[0].Pillars.Add(new FlickerVoid.Engine.Buildings.Plans.PlanPillar { Id = 40, X = 1, Z = 1 }));

        _builder.Load().GetParts().Select(static part => part.Id).Should().Contain("storey.0.pillar.40");
        UndoHistory history = _builder.Content.GetHistory(BuildingDocumentTestsBuilder.House);
        history.Undo();
        _builder.Load().GetParts().Select(static part => part.Id).Should().NotContain("storey.0.pillar.40");
    }

    [Fact]
    public void KeepsAPartTheMeshToolsReshapedAsItIs()
    {
        BuildingDocument document = _builder.Build();
        SolidMesh box = SolidMesh.Box(new System.Numerics.Vector3(1, 1, 1));

        document.SetMesh("storey.0.floor", box);
        document.Change(static building => building.Storeys[0].Height = 4);

        BuildingFile loaded = _builder.Load();
        loaded.Storeys[0].Free.Should().Equal("floor");
        loaded.Storeys[0].Parts["floor"].Mesh.Faces.Should().HaveCount(6, "the plan no longer makes it");
    }
}
