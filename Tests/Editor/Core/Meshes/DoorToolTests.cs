using System.Numerics;

using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Solids;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class DoorToolTests
{
    private const int Side = 0;

    private readonly VehicleFile _car = VehicleFile.Create(VehicleType.Car);

    private SolidMesh Body => _car.Body.Mesh;

    private SolidEdge FrontEdge => new SolidTopology(Body).GetFaceEdges(Side).First();

    [Fact]
    public void HangingADoorSetsAPocketInTheFaceAndFillsItWithADoorPart()
    {
        int points = Body.Points.Count;

        string? id = DoorTool.Hang(_car, "body", Side, FrontEdge, new DoorOptions { Thickness = 0.1f });

        id.Should().Be("door.1");
        _car.Doors.Should().ContainSingle();
        _car.Body.Mesh.Points.Should().HaveCount(points + 4);
        _car.Doors[0].Toggle.Should().NotBeNull();
        _car.Doors[0].Joint!.Value.Kind.Should().Be(JointKind.Hinge);
        _car.Doors[0].Place.Should().Be(_car.Body.Place);
    }

    [Fact]
    public void TheDoorIsTheFacesOutlineSetInByItsThickness()
    {
        DoorTool.Hang(_car, "body", Side, FrontEdge, new DoorOptions { Thickness = 0.2f });

        SolidMesh door = _car.Doors[0].Mesh;

        door.Points.Should().HaveCount(8);
        door.Faces.Should().HaveCount(6);
        float depth = door.Points.Max(point => point.X) - door.Points.Min(point => point.X);
        depth.Should().BeApproximately(0.2f, 1e-4f);
    }

    [Fact]
    public void TheHingeTurnsAboutTheEdgeAndSwingsTheDoorOutwardOrInward()
    {
        SolidMesh body = Body;
        SolidEdge edge = FrontEdge;
        Vector3 normal = Vector3.UnitX;
        IReadOnlyList<int> face = body.Faces[Side];
        Vector3 centre = face.Aggregate(Vector3.Zero, (sum, point) => sum + body.Points[point]) / face.Count;
        float facing = Vector3.Dot(centre, normal);
        facing.Should().BeGreaterThan(0, "face 0 is the +X side");

        DoorTool.Hang(_car, "body", Side, edge, new DoorOptions { Angle = 80, Outward = true });
        float outward = GetSwing(_car.Doors[0], centre);
        VehicleFile inwardCar = VehicleFile.Create(VehicleType.Car);
        DoorTool.Hang(inwardCar, "body", Side, new SolidTopology(inwardCar.Body.Mesh).GetFaceEdges(Side).First(), new DoorOptions { Outward = false });
        float inward = GetSwing(inwardCar.Doors[0], centre);

        _car.Doors[0].Joint!.Value.Max.Should().Be(80);
        outward.Should().BeGreaterThan(0);
        inward.Should().BeLessThan(0);
    }

    [Fact]
    public void ADoorOnTheSameHingeIsReplacedNotAdded()
    {
        VehicleFile fresh = VehicleFile.Create(VehicleType.Car);
        SolidEdge edge = new SolidTopology(fresh.Body.Mesh).GetFaceEdges(Side).First();
        DoorTool.Hang(fresh, "body", Side, edge, new DoorOptions());
        VehiclePart first = fresh.Doors[0];
        VehicleFile again = VehicleFile.Create(VehicleType.Car);
        again.Doors.Add(first);

        string? id = DoorTool.Hang(again, "body", Side, edge, new DoorOptions { Angle = 45 });

        id.Should().Be("door.1");
        again.Doors.Should().ContainSingle();
        again.Doors[0].Joint!.Value.Max.Should().Be(45);
    }

    [Fact]
    public void RefittingChangesTheAngleAndCanTurnTheDoorTheOtherWay()
    {
        DoorTool.Hang(_car, "body", Side, FrontEdge, new DoorOptions());
        PartJoint before = _car.Doors[0].Joint!.Value;

        bool refitted = DoorTool.Refit(_car, "door.1", 120, reverse: true);

        refitted.Should().BeTrue();
        PartJoint after = _car.Doors[0].Joint!.Value;
        after.Max.Should().Be(120);
        after.Axis.Should().Be(-before.Axis);
        after.Pivot.Should().Be(before.Pivot);
    }

    [Fact]
    public void ThingsThatCantHoldADoorAreRefused()
    {
        SolidEdge edge = FrontEdge;
        DoorTool.Hang(_car, "roof", Side, edge, new DoorOptions()).Should().BeNull();
        DoorTool.Hang(_car, "body", 99, edge, new DoorOptions()).Should().BeNull();
        DoorTool.Hang(_car, "body", Side, SolidEdge.Of(0, 7), new DoorOptions()).Should().BeNull();
        DoorTool.Hang(_car, "body", Side, edge, new DoorOptions { Thickness = 0 }).Should().BeNull();
        DoorTool.Hang(_car, "body", Side, edge, new DoorOptions { Angle = 200 }).Should().BeNull();
        DoorTool.Refit(_car, "body", 90, reverse: false).Should().BeFalse();
        _car.Doors.Should().BeEmpty();
    }

    // How far the door's face centre moves along the face's outward direction (+X) when the door stands open.
    private static float GetSwing(VehiclePart door, Vector3 centre)
    {
        PartJoint joint = door.Joint!.Value;
        Transform opened = JointSystem.Pose(door.Place, joint, joint.GetOpenPosition());
        Vector3 moved = Vector3.Transform(centre, opened.ToMatrix());
        Vector3 rest = Vector3.Transform(centre, door.Place.ToMatrix());
        return moved.X - rest.X;
    }
}
