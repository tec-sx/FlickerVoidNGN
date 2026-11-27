using FlickerVoid.Editor.Assets.Foliage;
using FlickerVoid.Engine.Foliage;

namespace FlickerVoid.Editor.Assets.Tests.Foliage;

public sealed class FoliagePanelViewModelTests : IDisposable
{
    private readonly FoliagePanelViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsANewTree()
    {
        FoliagePanelViewModel panel = _builder.Build();

        panel.Kind.Should().Be("Foliage");
        panel.Type.Should().Be(FoliageKind.Tree);
        panel.IsTree.Should().BeTrue();
        panel.Seed.Should().Be(1);
        panel.Height.Should().Be(6);
        panel.Limbs.Should().Be(4);
        panel.Crowns.Should().Be(5);
        panel.Parts.Should().HaveCount(10).And.StartWith("trunk");
    }

    [Fact]
    public void WritesTheShapeToTheFileAndShowsTheNewParts()
    {
        FoliagePanelViewModel panel = _builder.Build();

        panel.Limbs = 2;
        panel.Crowns = 3;
        panel.Height = 9;

        FoliageFile held = _builder.Load();
        held.Shape.Height.Should().Be(9);
        held.Limbs.Should().HaveCount(2);
        panel.Parts.Should().Equal("trunk", "limb.1", "limb.2", "crown.1", "crown.2", "crown.3");
    }

    [Fact]
    public void PutsBackANumberNothingGrowsFrom()
    {
        FoliagePanelViewModel panel = _builder.Build();

        panel.Crowns = 40;
        panel.Height = -1;

        panel.Crowns.Should().Be(5);
        panel.Height.Should().Be(6);
        _builder.Load().Shape.Crowns.Should().Be(5);
    }

    [Fact]
    public void ABushHasNoTrunkOrLimbsToSet()
    {
        FoliagePanelViewModel panel = _builder.Build();

        panel.Type = FoliageKind.Bush;

        panel.IsTree.Should().BeFalse();
        panel.Height.Should().Be(1);
        panel.Parts.Should().Equal("crown.1", "crown.2", "crown.3");
        _builder.Load().Kind.Should().Be(FoliageKind.Bush);
    }

    [Fact]
    public void ANewSeedGrowsAnotherPlantOfTheSameShape()
    {
        FoliagePanelViewModel panel = _builder.Build();

        panel.NewSeedCommand.Execute(null);

        panel.Seed.Should().NotBe(1);
        _builder.Load().Seed.Should().Be(panel.Seed);
        panel.Parts.Should().HaveCount(10);
    }
}
