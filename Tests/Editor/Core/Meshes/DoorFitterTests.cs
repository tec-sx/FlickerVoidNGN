using System.Numerics;

using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Solids;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class DoorFitterTests
{
    private readonly VehicleFile _car = VehicleFile.Create(VehicleType.Car);

    private DoorFit MakeFit()
    {
        SolidMesh body = _car.Body.Mesh;
        float side = body.Points.Max(point => point.X);
        float floor = body.Points.Min(point => point.Y);
        return new DoorFit { Wall = "body", Centre = new Vector3(side, floor + 0.1f, 0), Normal = Vector3.UnitX, Right = -Vector3.UnitZ, Width = 0.5f, Height = 0.4f, Thickness = 0.05f };
    }

    [Fact]
    public void PlacingADoorKeepsTheUncutWallAndCutsTheMeshFromIt()
    {
        Assert.SkipUnless(DoorFitter.IsAvailable, "Manifold's library is not built.");
        SolidMesh before = _car.Body.Mesh;

        string? id = DoorFitter.Place(_car, MakeFit());

        id.Should().Be("door.1");
        _car.Body.Source.Should().BeSameAs(before);
        _car.Body.Mesh.Points.Count.Should().BeGreaterThan(before.Points.Count);
        _car.Doors.Should().ContainSingle();
        _car.Doors[0].Joint!.Value.Kind.Should().Be(JointKind.Hinge);
        _car.Doors[0].Toggle.Should().NotBeNull();
    }

    [Fact]
    public void RemovingTheDoorClosesTheWallAndDropsItsSource()
    {
        Assert.SkipUnless(DoorFitter.IsAvailable, "Manifold's library is not built.");
        SolidMesh before = _car.Body.Mesh;
        DoorFitter.Place(_car, MakeFit());

        bool removed = DoorFitter.Remove(_car, "door.1");

        removed.Should().BeTrue();
        _car.Doors.Should().BeEmpty();
        _car.Body.Mesh.Should().BeSameAs(before);
        _car.Body.Source.Should().BeNull();
    }

    [Fact]
    public void ChangingTheFitAndRebuildingMovesTheCut()
    {
        Assert.SkipUnless(DoorFitter.IsAvailable, "Manifold's library is not built.");
        DoorFit fit = MakeFit();
        DoorFitter.Place(_car, fit);
        SolidMesh first = _car.Body.Mesh;

        fit.Width = 0.8f;
        DoorFitter.Rebuild(_car).Should().BeTrue();

        _car.Body.Mesh.Should().NotBeSameAs(first);
        _car.Body.Source.Should().NotBeNull();
        float width = _car.Doors[0].Mesh.Points.Max(point => point.Z) - _car.Doors[0].Mesh.Points.Min(point => point.Z);
        width.Should().BeApproximately(0.8f, 1e-3f);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void TheHingeSwingsTheFreeEdgeOutOfTheFaceForAnOutwardDoor(bool hingeLeft, bool outward)
    {
        Assert.SkipUnless(DoorFitter.IsAvailable, "Manifold's library is not built.");
        DoorFit fit = MakeFit();
        fit.HingeLeft = hingeLeft;
        fit.Outward = outward;
        DoorFitter.Place(_car, fit);

        PartJoint joint = _car.Doors[0].Joint!.Value;

        float swing = Vector3.Dot(Vector3.Cross(joint.Axis, hingeLeft ? fit.Right : -fit.Right), fit.Normal);
        int expected = 1;
        if (!outward)
        {
            expected = -1;
        }

        MathF.Sign(swing).Should().Be(expected);
    }

    [Fact]
    public void AWindowTakesAHoleOutOfTheDoorAndAFrameAddsToTheWall()
    {
        Assert.SkipUnless(DoorFitter.IsAvailable, "Manifold's library is not built.");
        DoorFit plain = MakeFit();
        DoorFitter.Place(_car, plain);
        int plainDoor = _car.Doors[0].Mesh.Points.Count;
        int plainWall = _car.Body.Mesh.Points.Count;
        plain.WindowWidth = 0.2f;
        plain.WindowHeight = 0.1f;
        plain.WindowFrom = 0.15f;
        plain.FrameWidth = 0.05f;

        DoorFitter.Rebuild(_car).Should().BeTrue();

        _car.Doors[0].Mesh.Points.Count.Should().BeGreaterThan(plainDoor);
        _car.Body.Mesh.Points.Count.Should().BeGreaterThan(plainWall);
    }

    [Fact]
    public void ADoorIsReachedAtItsGrabPoint()
    {
        Assert.SkipUnless(DoorFitter.IsAvailable, "Manifold's library is not built.");
        DoorFit fit = MakeFit();
        fit.GrabHeight = 0.2f;
        fit.GrabInset = 0.05f;
        DoorFitter.Place(_car, fit);

        Vector3 middle = _car.Doors[0].GetMiddle();

        middle.Should().Be(fit.GetGrab());
        Vector3.Dot(middle - fit.Centre, fit.Up).Should().BeApproximately(0.2f, 1e-4f);
    }

    [Fact]
    public void ADoorCannotBePlacedOnAPartThatMovesOrIsMissing()
    {
        Assert.SkipUnless(DoorFitter.IsAvailable, "Manifold's library is not built.");
        DoorFit fit = MakeFit();
        fit.Wall = "nothing";

        DoorFitter.Place(_car, fit).Should().BeNull();
        _car.Doors.Should().BeEmpty();
    }
}
