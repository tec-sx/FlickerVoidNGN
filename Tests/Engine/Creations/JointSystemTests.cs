using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Creations.Tests;

public sealed class JointSystemTests : IDisposable
{
    private const float Precision = 0.0001f;

    private readonly JointSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void StartsAtItsRestPoseWithinItsLimits()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 30, 90, 90);

        _builder.Place(new Vector3(2, 0, 0), hinge);

        _builder.PositionOfJoint().Should().Be(30);
        ShouldStandAt(Swung(30), RotationOf(30));
    }

    [Fact]
    public void HingeSwingsItsPartAboutThePivotTowardItsTargetAtItsSpeed()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);
        _builder.Place(new Vector3(2, 0, 0), hinge);

        _builder.Aim(90);
        _builder.Tick(0.5f);

        _builder.PositionOfJoint().Should().BeApproximately(45, Precision);
        ShouldStandAt(Swung(45), RotationOf(45));
    }

    [Fact]
    public void StopsAtItsTarget()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);
        _builder.Place(new Vector3(2, 0, 0), hinge);

        _builder.Aim(60);
        _builder.Tick(2);

        _builder.PositionOfJoint().Should().BeApproximately(60, Precision);
    }

    [Fact]
    public void StopsAtItsLimits()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), -20, 90, 90);
        _builder.Place(new Vector3(2, 0, 0), hinge);

        _builder.Aim(200);
        _builder.Tick(5);
        float opened = _builder.PositionOfJoint();
        _builder.Aim(-200);
        _builder.Tick(5);

        opened.Should().BeApproximately(90, Precision);
        _builder.PositionOfJoint().Should().BeApproximately(-20, Precision);
    }

    [Fact]
    public void SliderMovesItsPartAlongItsAxis()
    {
        PartJoint slider = JointSystemTestsBuilder.Slider(new Vector3(0, 0, 2), 0, 3, 1);
        _builder.Place(new Vector3(0, 1, 0), slider);

        _builder.Aim(3);
        _builder.Tick(2);

        ShouldStandAt(new Vector3(0, 1, 2), Quaternion.Identity);
    }

    [Fact]
    public void DoesNotMoveWhileTheGameIsPaused()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);
        _builder.Place(new Vector3(2, 0, 0), hinge);

        _builder.Aim(90);
        _builder.Tick(0.5f, paused: true);

        _builder.PositionOfJoint().Should().Be(0);
    }

    [Fact]
    public void SwungPartFollowsTheRoot()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);
        _builder.Place(new Vector3(2, 0, 0), hinge);
        _builder.Aim(90);
        _builder.Tick(1);

        _builder.MoveRoot(new Vector3(0, 0, 5));

        ShouldStandAt(Swung(90) + new Vector3(0, 0, 5), RotationOf(90));
    }

    [Fact]
    public void ADoorOpensWhenItsToggleOpens()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);
        _builder.PlaceDoor(hinge, open: false);

        _builder.Toggled(open: true);
        _builder.Tick(0.5f);
        float halfway = _builder.PositionOfJoint();
        _builder.Tick(1);

        halfway.Should().BeApproximately(45, Precision);
        _builder.PositionOfJoint().Should().BeApproximately(90, Precision);
    }

    [Fact]
    public void ADoorShutsWhenItsToggleCloses()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);
        _builder.PlaceDoor(hinge, open: true);

        _builder.Toggled(open: false);
        _builder.Tick(2);

        _builder.PositionOfJoint().Should().BeApproximately(0, Precision);
    }

    [Fact]
    public void ADoorThatStartsOpenStandsOpen()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);

        _builder.PlaceDoor(hinge, open: true);

        _builder.PositionOfJoint().Should().Be(90);
        ShouldStandAt(Swung(90), RotationOf(90));
    }

    [Fact]
    public void ADoorThatOpensTheOtherWayOpensToItsFarLimit()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), -80, 0, 90);
        _builder.PlaceDoor(hinge, open: false);

        _builder.Toggled(open: true);
        _builder.Tick(2);

        _builder.PositionOfJoint().Should().BeApproximately(-80, Precision);
    }

    [Fact]
    public void AToggleOnSomethingWithoutAJointIsIgnored()
    {
        PartJoint hinge = JointSystemTestsBuilder.Hinge(new Vector3(1, 0, 0), 0, 90, 90);
        _builder.PlaceDoor(hinge, open: false);

        _builder.Toggled(_builder.Root, open: true);
        _builder.Tick(2);

        _builder.PositionOfJoint().Should().Be(0);
    }

    private static Quaternion RotationOf(float degrees)
    {
        return Quaternion.CreateFromAxisAngle(Vector3.UnitY, float.DegreesToRadians(degrees));
    }

    /// <summary>Where a part resting at (2, 0, 0) stands when swung by the degrees about the pivot at (1, 0, 0).</summary>
    private static Vector3 Swung(float degrees)
    {
        Quaternion rotation = RotationOf(degrees);
        return new Vector3(1, 0, 0) + Vector3.Transform(Vector3.UnitX, rotation);
    }

    private void ShouldStandAt(Vector3 position, Quaternion rotation)
    {
        Transform transform = _builder.TransformOfPart();
        Vector3.Distance(transform.Position, position).Should().BeLessThan(Precision);
        MathF.Abs(Quaternion.Dot(transform.Rotation, rotation)).Should().BeApproximately(1, Precision);
    }
}
