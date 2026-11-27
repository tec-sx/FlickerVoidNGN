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

        panel.Types.Should().Equal(VehicleType.Car, VehicleType.Motorcycle, VehicleType.Trailer);
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
    public void ShowsTheDriveAVehicleOfItsTypeStartsWith()
    {
        VehiclePanelViewModel panel = _builder.Build();

        panel.Mass.Should().Be(1500);
        panel.EngineTorque.Should().Be(500);
        panel.Driven.Should().Be(DrivenAxles.Rear);
        panel.Steer.Should().Be(35);
        panel.DrivenChoices.Should().Equal(DrivenAxles.Rear, DrivenAxles.Front, DrivenAxles.All);
        _builder.Content.IsUnsaved("Van.fvvehicle").Should().BeFalse();
    }

    [Fact]
    public void ChangingADriveValueWritesTheDriveToTheFile()
    {
        VehiclePanelViewModel panel = _builder.Build();

        panel.Mass = 900;
        panel.Driven = DrivenAxles.All;

        VehicleDrive drive = _builder.Load().GetDrive();
        drive.Mass.Should().Be(900);
        drive.Driven.Should().Be(DrivenAxles.All);
        drive.EngineTorque.Should().Be(500, "the values left alone are kept");
    }

    [Fact]
    public void PutsBackADriveValueAVehicleCantDriveWith()
    {
        VehiclePanelViewModel panel = _builder.Build();

        panel.Mass = 0;
        panel.SuspensionDamping = 2;

        panel.Mass.Should().Be(1500);
        panel.SuspensionDamping.Should().Be(0.5f);
        _builder.Content.IsUnsaved("Van.fvvehicle").Should().BeFalse();
    }

    [Fact]
    public void RefusesAWheelCountTheTypeDoesNotTake()
    {
        VehiclePanelViewModel panel = _builder.Build();

        panel.WheelCount = 5;

        panel.WheelCount.Should().Be(4);
        _builder.Content.IsUnsaved("Van.fvvehicle").Should().BeFalse();
    }

    [Fact]
    public void WithoutALookItSaysHowToMakeOne()
    {
        VehiclePanelViewModel panel = _builder.BuildWithLooks();

        panel.HasLookTools.Should().BeTrue();
        panel.HasLook.Should().BeFalse();
        panel.LookReport.Should().Contain("Open in Blender");
    }

    [Fact]
    public void TheLookReportSaysWhatItCoversWhatKeepsItsGreyboxAndWhatJoinedTheBody()
    {
        _builder.WithLook(["body"], ["wheel.1L"], ["Antenna"]);

        VehiclePanelViewModel panel = _builder.BuildWithLooks();

        panel.HasLook.Should().BeTrue();
        panel.LookReport.Should().Be("Look on 2 of 5 parts. Greybox kept: wheel.1R, wheel.2L, wheel.2R. Joined the body: Antenna.");
    }

    [Fact]
    public void OpenInBlenderHandsOverTheVehicleAndShowsWhatWasSaid()
    {
        VehiclePanelViewModel panel = _builder.BuildWithLooks();
        _builder.Looks.OpenInBlender(VehicleDocumentTestsBuilder.Van, Arg.Any<VehicleFile>()).Returns("Opened in Blender.");

        panel.OpenInBlenderCommand.Execute(null);

        panel.BlenderStatus.Should().Be("Opened in Blender.");
    }

    [Fact]
    public void TheLookSwitchShowsTheGreyboxesAndTheLookAgain()
    {
        VehiclePanelViewModel panel = _builder.BuildWithLooks();
        bool shown = panel.ShowsLook;

        panel.ShowsLook = false;

        shown.Should().BeTrue();
        _builder.Looks.ShowGreyboxes.Should().BeTrue();
        panel.ShowsLook.Should().BeFalse();
    }

    [Fact]
    public void ALookBuiltWhileThePanelIsOpenShowsWhenItIsReadAgain()
    {
        VehiclePanelViewModel panel = _builder.BuildWithLooks();
        _builder.WithLook(["body"]);

        panel.RefreshLook();

        panel.HasLook.Should().BeTrue();
    }
}
