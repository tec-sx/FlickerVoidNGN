using FlickerVoid.Editor.Assets.Buildings;
using FlickerVoid.Editor.Core.Buildings;

namespace FlickerVoid.Editor.Assets.Tests.Buildings;

public sealed class BuildingPanelViewModelTests : IDisposable
{
    private readonly BuildingPanelViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsANewBuildingsGroundFloorAndItsPartsAsATree()
    {
        BuildingPanelViewModel panel = _builder.Build();

        panel.Kind.Should().Be("Building");
        panel.Storeys.Select(static storey => storey.Name).Should().Equal("Ground");
        panel.Level.Should().Be(0);
        panel.Nodes.Select(static node => node.Name).Should().Equal("House.fvbuilding", "Ground", "Floor", "Outside walls", "Hall", "Front door", "Roof");
        panel.Nodes.Select(static node => node.Depth).Should().Equal(0, 1, 2, 2, 2, 2, 1);
        panel.Nodes.Single(static node => node.Name == "Front door").PartId.Should().Be("storey.0.door.9");
        panel.Nodes.Single(static node => node.Name == "Front door").Pick.Should().Be(new PlanPick(PlanPickKind.Opening, 0, 9));
        panel.RemoveStoreyCommand.CanExecute(null).Should().BeFalse("the ground floor stays");
    }

    [Fact]
    public void AddsStoreysAboveAndACellarStackedTopFirstAndEditsTheNewOne()
    {
        BuildingPanelViewModel panel = _builder.Build();
        List<int> levels = [];
        panel.LevelChanged += levels.Add;

        panel.AddStoreyAboveCommand.Execute(null);
        panel.AddStoreyAboveCommand.Execute(null);
        panel.AddCellarCommand.Execute(null);

        panel.Storeys.Select(static storey => storey.Name).Should().Equal("2", "1", "Ground", "Cellar");
        panel.Level.Should().Be(-1);
        levels.Should().Equal(1, 2, -1);
        _builder.Load().StoreysAbove.Should().Be(3);
        _builder.Load().HasCellar.Should().BeTrue();
        panel.Nodes.Select(static node => node.Name).Should().Contain(["Storey 1", "Storey 2", "Cellar", "Landing"]);
    }

    [Fact]
    public void DuplicatesTheStoreyBeingEditedOnTopWithItsOpenings()
    {
        BuildingPanelViewModel panel = _builder.Build();

        panel.DuplicateStoreyCommand.Execute(null);

        panel.Level.Should().Be(1);
        _builder.Load().Storeys[1].Openings.Should().ContainSingle(static opening => opening.Name == "Front door");
    }

    [Fact]
    public void RemovesTheStoreyBeingEditedAndEditsTheOneBelow()
    {
        BuildingPanelViewModel panel = _builder.Build();
        panel.AddStoreyAboveCommand.Execute(null);

        panel.RemoveStoreyCommand.Execute(null);

        panel.Level.Should().Be(0);
        _builder.Load().Storeys.Should().ContainSingle();
    }
}
