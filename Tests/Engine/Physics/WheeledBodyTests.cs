using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Physics.Tests;

public sealed class WheeledBodyTests : IDisposable
{
    private readonly WheeledBodyTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ACarRestsOnItsWheels()
    {
        Entity car = _builder.PlaceCar();

        _builder.Run(2);

        Vector3 position = _builder.PositionOf(car);
        position.Y.Should().BeInRange(0.15f, 0.6f, "the suspension holds the body off the floor near where it was placed");
        float along = new Vector2(position.X, position.Z).Length();
        along.Should().BeLessThan(0.1f, "it stands still");
        for (int wheel = 0; wheel < 4; wheel++)
        {
            _builder.Physics.IsWheelOnGround(car, wheel).Should().BeTrue();
        }
    }

    [Fact]
    public void ForwardInputDrivesItTheWayItFaces()
    {
        Entity car = _builder.PlaceCar();

        _builder.Drive(car, forward: 1);
        _builder.Run(3);

        Vector3 position = _builder.PositionOf(car);
        position.Z.Should().BeGreaterThan(3);
        MathF.Abs(position.X).Should().BeLessThan(0.5f);
    }

    [Fact]
    public void ItsVelocityFollowsTheWayItDrives()
    {
        Entity car = _builder.PlaceCar();
        _builder.Physics.GetVelocity(car).Length().Should().BeLessThan(0.1f);

        _builder.Drive(car, forward: 1);
        _builder.Run(2);

        _builder.Physics.GetVelocity(car).Z.Should().BeGreaterThan(1);
    }

    [Fact]
    public void BackwardInputReversesIt()
    {
        Entity car = _builder.PlaceCar();

        _builder.Drive(car, forward: -1);
        _builder.Run(3);

        _builder.PositionOf(car).Z.Should().BeLessThan(-1);
    }

    [Fact]
    public void RightInputSteersItToItsRight()
    {
        Entity car = _builder.PlaceCar();

        _builder.Drive(car, forward: 1, right: 1);
        _builder.Run(3);

        _builder.PositionOf(car).X.Should().BeLessThan(-0.5f, "its left is +X, so its right is -X");
    }

    [Fact]
    public void BrakingStopsIt()
    {
        Entity car = _builder.PlaceCar();
        _builder.Drive(car, forward: 1);
        _builder.Run(2);

        _builder.Drive(car, forward: 0, brake: 1);
        _builder.Run(3);
        Vector3 stopped = _builder.PositionOf(car);
        _builder.Run(1);

        float crept = Vector3.Distance(stopped, _builder.PositionOf(car));
        crept.Should().BeLessThan(0.05f);
    }

    [Fact]
    public void WheelsTurnAsItDrivesAndSteerWithTheInput()
    {
        Entity car = _builder.PlaceCar();
        _builder.Physics.TryGetWheelPose(car, 0, out Transform rest).Should().BeTrue();

        _builder.Drive(car, forward: 1, right: 1);
        _builder.Run(1);
        _builder.Physics.TryGetWheelPose(car, 0, out Transform front);
        _builder.Physics.TryGetWheelPose(car, 2, out Transform rear);

        rest.Position.X.Should().BeApproximately(0.8f, 0.01f);
        rest.Position.Z.Should().BeApproximately(1.3f, 0.01f);
        rest.Position.Y.Should().BeInRange(-0.2f, 0.2f, "the wheel hangs near where it was placed");
        front.Rotation.Should().NotBe(rest.Rotation);
        Vector3 frontAxle = Vector3.Transform(Vector3.UnitX, front.Rotation);
        Vector3 rearAxle = Vector3.Transform(Vector3.UnitX, rear.Rotation);
        MathF.Abs(frontAxle.Z).Should().BeGreaterThan(0.2f, "the front wheels are steered");
        MathF.Abs(rearAxle.Z).Should().BeLessThan(0.05f, "the rear wheels are not");
    }

    [Fact]
    public void AnEntityWithoutWheelsHasNoWheelPoses()
    {
        Entity car = _builder.PlaceCar();
        Entity other = _builder.Game.World.Create(new Transform());

        _builder.Physics.TryGetWheelPose(other, 0, out _).Should().BeFalse();
        _builder.Physics.TryGetWheelPose(car, 9, out _).Should().BeFalse();
        _builder.Physics.IsWheelOnGround(other, 0).Should().BeFalse();
    }

    [Fact]
    public void TakingTheWheeledBodyOffTakesItOutOfTheSimulation()
    {
        Entity car = _builder.PlaceCar();
        int bodies = _builder.Physics.BodyCount;

        _builder.Game.World.Remove<WheeledBody>(car);
        _builder.Run(0.1f);

        _builder.Physics.BodyCount.Should().Be(bodies - 1);
        _builder.Physics.HasBody(car).Should().BeFalse();
    }
}
