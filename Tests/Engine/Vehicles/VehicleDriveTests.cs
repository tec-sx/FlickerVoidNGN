using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Vehicles.Tests;

public sealed class VehicleDriveTests : IDisposable
{
    private VehicleDriveTestsBuilder? _builder;

    public void Dispose() => _builder?.Dispose();

    [Fact]
    public void AVehicleWithADriverBecomesABodyOnItsFilesWheels()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        _builder = new VehicleDriveTestsBuilder(car);
        Entity vehicle = _builder.Place();

        _builder.Enter(vehicle);

        WheeledBody body = _builder.World.Get<WheeledBody>(vehicle);
        body.Collider.Should().BeOfType<HullCollider>();
        body.Mass.Should().Be(1500);
        body.Leans.Should().BeFalse();
        body.Wheels.Should().HaveCount(4);
        body.Wheels[0].Position.Should().Be(car.Wheels[0].Place.Position);
        body.Wheels[0].Radius.Should().BeApproximately(0.35f, 0.001f);
        body.Wheels[0].Width.Should().BeApproximately(0.25f, 0.001f);
        body.Wheels[0].MaxSteer.Should().Be(35);
        body.Wheels[0].Driven.Should().BeFalse();
        body.Wheels[2].MaxSteer.Should().Be(0);
        body.Wheels[2].Driven.Should().BeTrue();
        body.Wheels[2].HandBrake.Should().BeTrue();
        _builder.Physics.HasBody(vehicle).Should().BeTrue();
    }

    [Fact]
    public void ADrivenVehiclesPartsGiveUpTheirStandingCollision()
    {
        _builder = new VehicleDriveTestsBuilder(VehicleFileTestsBuilder.WithDoor());
        Entity vehicle = _builder.Place();
        Entity body = _builder.Parts.Find(vehicle, "body");
        Entity door = _builder.Parts.Find(vehicle, "door.1");
        _builder.Physics.HasBody(body).Should().BeTrue();

        _builder.Enter(vehicle);

        _builder.World.Has<StaticBody>(body).Should().BeFalse();
        _builder.World.Has<RigidBody>(door).Should().BeFalse();
        _builder.Physics.HasBody(body).Should().BeFalse();
        _builder.Parts.Find(vehicle, "body").Should().Be(body, "the parts stay the same entities");
    }

    [Fact]
    public void ItDrivesTheWayItFacesAndItsWheelPartsRollAndSteer()
    {
        _builder = new VehicleDriveTestsBuilder(VehicleFile.Create(VehicleType.Car));
        Entity vehicle = _builder.Place();
        _builder.Enter(vehicle);
        Entity front = _builder.Parts.Find(vehicle, "wheel.1L");
        Entity rear = _builder.Parts.Find(vehicle, "wheel.2L");
        Quaternion rested = _builder.PlaceOf(rear).Rotation;

        _builder.Drive(vehicle, forward: 1, right: 1);
        _builder.Run(2);

        _builder.PlaceOf(vehicle).Position.Z.Should().BeGreaterThan(1);
        float rolled = Quaternion.Dot(rested, _builder.PlaceOf(rear).Rotation);
        MathF.Abs(rolled).Should().BeLessThan(0.999f, "the rear wheel turned as the car rolled");
        Vector3 frontAxle = Vector3.Transform(Vector3.UnitX, _builder.PlaceOf(front).Rotation);
        Vector3 rearAxle = Vector3.Transform(Vector3.UnitX, _builder.PlaceOf(rear).Rotation);
        float steered = Vector3.Dot(frontAxle, rearAxle);
        steered.Should().BeLessThan(0.95f, "the front wheel is turned to steer and the rear is not");
    }

    [Fact]
    public void AVehicleItsDriverLeftBrakesAndStandsSolidAgain()
    {
        _builder = new VehicleDriveTestsBuilder(VehicleFile.Create(VehicleType.Car));
        Entity vehicle = _builder.Place();
        _builder.Enter(vehicle);
        _builder.Drive(vehicle, forward: 1);
        _builder.Run(2);

        _builder.Leave(vehicle);
        _builder.Run(0.1f);
        _builder.World.Has<WheeledBody>(vehicle).Should().BeTrue("it is still rolling");
        _builder.Run(5);

        _builder.World.Has<WheeledBody>(vehicle).Should().BeFalse();
        _builder.World.Has<DriveInput>(vehicle).Should().BeFalse();
        Entity body = _builder.Parts.Find(vehicle, "body");
        _builder.World.Has<StaticBody>(body).Should().BeTrue();
        _builder.Physics.HasBody(body).Should().BeTrue();
        Vector3 stopped = _builder.PlaceOf(vehicle).Position;
        stopped.Z.Should().BeGreaterThan(1, "it stands where it stopped");
        _builder.Run(1);
        _builder.PlaceOf(vehicle).Position.Should().Be(stopped);
    }

    [Fact]
    public void AMotorcycleOnTwoWheelsStaysUpAsItDrives()
    {
        VehicleFile motorcycle = VehicleFile.Create(VehicleType.Motorcycle);
        _builder = new VehicleDriveTestsBuilder(motorcycle);
        Entity vehicle = _builder.Place();
        _builder.Enter(vehicle);
        _builder.World.Get<WheeledBody>(vehicle).Leans.Should().BeTrue();

        _builder.Drive(vehicle, forward: 1);
        _builder.Run(3);

        Transform place = _builder.PlaceOf(vehicle);
        place.Position.Z.Should().BeGreaterThan(1);
        Vector3 up = Vector3.Transform(Vector3.UnitY, place.Rotation);
        up.Y.Should().BeGreaterThan(0.9f);
    }

    [Fact]
    public void TheFilesDriveSetsTheEngineTheWeightAndTheDrivenAxles()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        car.Drive = new VehicleDrive { Mass = 900, EngineTorque = 300, Driven = DrivenAxles.Front, Steer = 20 };
        _builder = new VehicleDriveTestsBuilder(car);
        Entity vehicle = _builder.Place();

        _builder.Enter(vehicle);

        WheeledBody body = _builder.World.Get<WheeledBody>(vehicle);
        body.Mass.Should().Be(900);
        body.EngineTorque.Should().Be(300);
        body.Wheels[0].Driven.Should().BeTrue();
        body.Wheels[0].MaxSteer.Should().Be(20);
        body.Wheels[2].Driven.Should().BeFalse();
    }

    [Fact]
    public void ATrailerTakesNoDriver()
    {
        _builder = new VehicleDriveTestsBuilder(VehicleFile.Create(VehicleType.Trailer));
        Entity trailer = _builder.Place();
        VehicleDriveSystem drive = _builder.Game.Services.GetService<VehicleDriveSystem>()!;

        _builder.Enter(trailer);

        drive.CanDrive(trailer).Should().BeFalse();
        _builder.World.Has<WheeledBody>(trailer).Should().BeFalse();
        _builder.World.Has<DrivenVehicle>(trailer).Should().BeFalse();
    }

    [Fact]
    public void ACarTakesADriver()
    {
        _builder = new VehicleDriveTestsBuilder(VehicleFile.Create(VehicleType.Car));
        Entity car = _builder.Place();

        _builder.Game.Services.GetService<VehicleDriveSystem>()!.CanDrive(car).Should().BeTrue();
    }

    [Fact]
    public void AFileKeepsItsDriveAndOneWithoutDrivesAsItsTypeDoes()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        VehicleFileTestsBuilder.Write(car).Should().NotContain("\"drive\"");
        car.Drive = new VehicleDrive { Mass = 900, Driven = DrivenAxles.All };

        string json = VehicleFileTestsBuilder.Write(car);
        VehicleFile read = VehicleFileTestsBuilder.Read(json);

        json.Should().Contain("\"driven\":\"all\"");
        read.GetDrive().Mass.Should().Be(900);
        read.GetDrive().Driven.Should().Be(DrivenAxles.All);
        VehicleFile.Create(VehicleType.Motorcycle).GetDrive().Mass.Should().Be(250);
    }
}
