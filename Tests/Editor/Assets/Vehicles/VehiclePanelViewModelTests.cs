using FlickerVoid.Editor.Assets.Vehicles;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Assets.Tests.Vehicles;

public sealed class VehiclePanelViewModelTests : IDisposable
{
    private readonly VehiclePanelViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsTheTypeTheWheelCountsItTakesAndTheParts()
    {
        VehiclePanelViewModel panel = _builder.Build();

        panel.Types.Should().Equal(VehicleType.Car, VehicleType.Motorcycle);
        panel.Type.Should().Be(VehicleType.Car);
        panel.WheelCounts.Should().Equal(4, 6, 8);
        panel.WheelCount.Should().Be(4);
        panel.Parts.Should().Equal("body", "wheel.1L", "wheel.1R", "wheel.2L", "wheel.2R");
    }

    [Fact]
    public void ChangesTheWheelCountInTheFile()
    {
        VehiclePanelViewModel panel = _builder.Build();
        List<string?> changed = [];
        panel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        panel.WheelCount = 6;

        _builder.Load().Wheels.Should().HaveCount(6);
        panel.Parts.Should().HaveCount(7).And.Contain(["wheel.3L", "wheel.3R"]);
        changed.Should().Contain(nameof(VehiclePanelViewModel.Parts));
    }

    [Fact]
    public void OffersTheWheelCountsOfTheNewType()
    {
        VehiclePanelViewModel panel = _builder.Build();
        panel.WheelCount = 8;

        panel.Type = VehicleType.Motorcycle;

        panel.WheelCounts.Should().Equal(2, 4);
        panel.WheelCount.Should().Be(4, "the nearest count a motorcycle takes");
        _builder.Load().Type.Should().Be(VehicleType.Motorcycle);
        panel.Parts.Should().HaveCount(5);
    }

    [Fact]
    public void RefusesAWheelCountTheTypeDoesNotTake()
    {
        VehiclePanelViewModel panel = _builder.Build();

        panel.WheelCount = 5;

        panel.WheelCount.Should().Be(4);
        _builder.Content.IsUnsaved("Van.fvvehicle").Should().BeFalse();
    }
}
