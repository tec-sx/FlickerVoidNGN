using FlickerVoid.Editor.Assets.Buildings;

namespace FlickerVoid.Editor.Assets.Tests.Buildings;

public sealed class BuildingPanelViewModelTests : IDisposable
{
    private readonly BuildingPanelViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsANewBuilding()
    {
        BuildingPanelViewModel panel = _builder.Build();

        panel.Kind.Should().Be("Building");
        panel.Storeys.Should().Be(1);
        panel.HasCellar.Should().BeFalse();
        panel.HasElevator.Should().BeFalse();
        panel.Parts.Should().Equal("storey.0", "roof");
        panel.StairsFrom.Should().BeEmpty();
        panel.AddStairsCommand.CanExecute(null).Should().BeFalse("no storey is above the ground floor");
    }

    [Fact]
    public void WritesStoreysAndTheCellarToTheFile()
    {
        BuildingPanelViewModel panel = _builder.Build();

        panel.Storeys = 3;
        panel.HasCellar = true;

        _builder.Load().StoreysAbove.Should().Be(3);
        _builder.Load().HasCellar.Should().BeTrue();
        panel.Parts.Should().Equal("cellar", "storey.0", "storey.1", "storey.2", "roof");
        panel.StairsFrom.Should().Equal("cellar", "storey.0", "storey.1");
    }

    [Fact]
    public void PutsBackACountABuildingCannotTake()
    {
        BuildingPanelViewModel panel = _builder.Build();

        panel.Storeys = 40;

        panel.Storeys.Should().Be(1);
        _builder.Load().StoreysAbove.Should().Be(1);
    }

    [Fact]
    public void AddsStairsFromTheChosenStorey()
    {
        BuildingPanelViewModel panel = _builder.Build();
        panel.Storeys = 3;
        panel.StairsStorey = "storey.1";

        panel.AddStairsCommand.Execute(null);

        panel.Parts.Should().Contain("stairs.1");
        float foot = _builder.Load().GetFloorHeight(1);
        _builder.Load().Stairs[0].Place.Position.Y.Should().BeApproximately(foot, 1e-4f);
        panel.StairsStorey.Should().Be("storey.1", "the choice stays for the next flight");
    }

    [Fact]
    public void AddsAndRemovesTheElevator()
    {
        BuildingPanelViewModel panel = _builder.Build();
        panel.Storeys = 2;

        panel.HasElevator = true;
        bool added = panel.Parts.Contains("elevator");
        panel.HasElevator = false;

        added.Should().BeTrue();
        panel.Parts.Should().NotContain("elevator");
    }
}
