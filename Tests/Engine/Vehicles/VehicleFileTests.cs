using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Runtime.Solids;
using FlickerVoid.Engine.Vehicles.Serialization;

namespace FlickerVoid.Engine.Vehicles.Tests;

public sealed class VehicleFileTests
{
    [Fact]
    public void ANewCarHasABodyAndFourWheelsOnTwoAxles()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);

        car.Type.Should().Be(VehicleType.Car);
        car.GetParts().Select(part => part.Id).Should().Equal("body", "wheel.1L", "wheel.1R", "wheel.2L", "wheel.2R");
        car.GetParts().Should().OnlyContain(part => part.Part.Mesh.Triangulate().Indices.Length > 0);
    }

    [Fact]
    public void ANewCarStandsOnItsWheelsWithTheFirstAxleAtTheFront()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);

        Vector3 frontLeft = car.Wheels[0].Place.Position;
        Vector3 frontRight = car.Wheels[1].Place.Position;
        Vector3 backLeft = car.Wheels[2].Place.Position;
        frontLeft.Z.Should().BeGreaterThan(backLeft.Z, "the front is along +Z");
        frontLeft.X.Should().BeGreaterThan(0, "left is +X, facing forward");
        frontRight.X.Should().Be(-frontLeft.X);
        float lowest = car.Wheels[0].Mesh.Points.Min(point => point.Y) + frontLeft.Y;
        lowest.Should().BeApproximately(0, 1e-4f, "the wheels stand on the ground");
        car.Body.Place.Position.Y.Should().BeGreaterThan(0, "the body stands above the ground");
    }

    [Fact]
    public void ANewMotorcycleHasTwoWheelsInLine()
    {
        VehicleFile motorcycle = VehicleFile.Create(VehicleType.Motorcycle);

        motorcycle.GetParts().Select(part => part.Id).Should().Equal("body", "wheel.1", "wheel.2");
        motorcycle.Wheels.Should().OnlyContain(wheel => wheel.Place.Position.X == 0);
    }

    [Theory]
    [InlineData(VehicleType.Car, new[] { 4, 6, 8 })]
    [InlineData(VehicleType.Motorcycle, new[] { 2, 4 })]
    public void EachTypeTakesItsWheelCounts(VehicleType type, int[] counts)
    {
        VehicleFile.GetWheelCounts(type).Should().Equal(counts);
    }

    [Fact]
    public void ChangingTheWheelCountAddsAxlesBehindAndKeepsEveryOtherWheel()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        VehiclePart body = car.Body;
        VehiclePart frontLeft = car.Wheels[0];
        frontLeft.Place = new Transform { Position = new Vector3(1, 0.4f, 1.5f) };

        car.SetWheelCount(6);

        car.Body.Should().BeSameAs(body);
        car.Wheels[0].Should().BeSameAs(frontLeft);
        car.GetParts().Select(part => part.Id).Should().Equal("body", "wheel.1L", "wheel.1R", "wheel.2L", "wheel.2R", "wheel.3L", "wheel.3R");
        car.Wheels[4].Place.Position.Z.Should().BeLessThan(car.Wheels[2].Place.Position.Z, "the new axle is behind the last");
        car.Wheels[4].Place.Position.X.Should().Be(car.Wheels[2].Place.Position.X);
    }

    [Fact]
    public void AddingAxlesMovesTheBodysRearBackWithTheLastAxle()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        float overhang = VehicleFileTestsBuilder.GetRearOverhang(car);
        (Vector3 low, Vector3 high) = VehicleFileTestsBuilder.GetBodyBox(car);

        car.SetWheelCount(8);

        (Vector3 lowAfter, Vector3 highAfter) = VehicleFileTestsBuilder.GetBodyBox(car);
        VehicleFileTestsBuilder.GetRearOverhang(car).Should().BeApproximately(overhang, 1e-4f, "the body reaches as far behind the last wheels as before");
        highAfter.Should().Be(high, "the front stays where it was");
        lowAfter.X.Should().Be(low.X);
        lowAfter.Y.Should().Be(low.Y);
        car.Body.Mesh.Triangulate().Indices.Length.Should().BeGreaterThan(0, "the body is still a closed solid");
    }

    [Fact]
    public void RemovingAxlesBringsTheBodysRearForwardAgain()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        Vector3[] before = [.. car.Body.Mesh.Points];
        car.SetWheelCount(8);

        car.SetWheelCount(4);

        Vector3[] after = [.. car.Body.Mesh.Points];
        after.Should().HaveCount(before.Length);
        for (int index = 0; index < before.Length; index++)
        {
            Vector3.Distance(after[index], before[index]).Should().BeLessThan(1e-4f);
        }
    }

    [Fact]
    public void OnlyTheBodyBehindTheLastAxleMoves()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        car.Body.Mesh = SolidMesh.Box(new Vector3(2, 2, 5));
        car.Body.Place = new Transform { Position = new Vector3(0, 0.5f, 0.25f) };
        float lastAxle = car.Wheels[^1].Place.Position.Z;
        Vector3[] before = [.. car.Body.Mesh.Points];

        car.SetWheelCount(6);

        float moved = car.Wheels[^1].Place.Position.Z - lastAxle;
        Vector3[] after = [.. car.Body.Mesh.Points];
        for (int index = 0; index < before.Length; index++)
        {
            Vector3 expected = before[index];
            if (before[index].Z + 0.25f < lastAxle)
            {
                expected.Z += moved;
            }

            Vector3.Distance(after[index], expected).Should().BeLessThan(1e-4f);
        }
    }

    [Fact]
    public void ChangingTheWheelCountDownRemovesTheLastAxles()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        car.SetWheelCount(8);
        VehiclePart[] first = [.. car.Wheels.Take(4)];

        car.SetWheelCount(4);

        car.Wheels.Should().Equal(first);
    }

    [Theory]
    [InlineData(VehicleType.Car, 5)]
    [InlineData(VehicleType.Car, 2)]
    [InlineData(VehicleType.Motorcycle, 6)]
    public void RefusesAWheelCountItsTypeDoesNotTake(VehicleType type, int count)
    {
        VehicleFile vehicle = VehicleFile.Create(type);

        Action change = () => vehicle.SetWheelCount(count);

        change.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ChangingTheTypeKeepsTheBodyAndTheWheelsThatStillFit()
    {
        VehicleFile vehicle = VehicleFile.Create(VehicleType.Car);
        float overhang = VehicleFileTestsBuilder.GetRearOverhang(vehicle);
        vehicle.SetWheelCount(6);
        VehiclePart body = vehicle.Body;
        VehiclePart[] first = [.. vehicle.Wheels.Take(4)];

        vehicle.SetType(VehicleType.Motorcycle);

        vehicle.Type.Should().Be(VehicleType.Motorcycle);
        vehicle.Body.Should().BeSameAs(body);
        vehicle.Wheels.Should().Equal(first, "a motorcycle takes four wheels at most, two to an axle");
        VehicleFileTestsBuilder.GetRearOverhang(vehicle).Should().BeApproximately(overhang, 1e-4f, "the body shortens with the axle taken off");
    }

    [Fact]
    public void ChangingTheTypeRemakesWheelsThatNoLongerSitTheSameWay()
    {
        VehicleFile vehicle = VehicleFile.Create(VehicleType.Motorcycle);
        VehiclePart body = vehicle.Body;

        vehicle.SetType(VehicleType.Car);

        vehicle.Body.Should().BeSameAs(body);
        vehicle.GetParts().Select(part => part.Id).Should().Equal("body", "wheel.1L", "wheel.1R", "wheel.2L", "wheel.2R");
    }

    [Fact]
    public void ReadsBackAsWritten()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        car.SetWheelCount(6);
        string written = VehicleFileTestsBuilder.Write(car);

        VehicleFile read = VehicleFileTestsBuilder.Read(written);

        VehicleFileTestsBuilder.Write(read).Should().Be(written);
        written.Should().Contain("\"type\":\"car\"");
        read.Wheels.Should().HaveCount(6);
    }

    [Fact]
    public void RefusesAFileWithAWheelCountItsTypeDoesNotTake()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        string written = VehicleFileTestsBuilder.Write(car);
        JsonNode file = JsonNode.Parse(written)!;
        file["wheels"]!.AsArray().RemoveAt(3);
        string json = file.ToJsonString();

        Action read = () => VehicleFileTestsBuilder.Read(json);

        read.Should().Throw<JsonException>().WithMessage("A car takes 4, 6 or 8 wheels, not 3.");
    }

    [Fact]
    public void RefusesAFileWhoseMeshIsOpen()
    {
        string json = VehicleFileTestsBuilder.WithOpenBody();

        Action read = () => VehicleFileTestsBuilder.Read(json);

        read.Should().Throw<JsonException>().WithMessage("The solid is open*");
    }

    [Fact]
    public void DescribesTheFileInItsSchema()
    {
        JsonObject schema = JsonSchemas.CreateFile(VehiclesJsonContext.Default.VehicleFile);

        JsonNode properties = schema["properties"]!;
        properties["type"]!["enum"]!.AsArray().Select(value => value!.GetValue<string>()).Should().Equal("car", "motorcycle");
        properties["body"]!["properties"]!["mesh"]!["required"]!.AsArray().Should().HaveCount(2);
        properties["wheels"]!["type"]!.ToString().Should().Contain("array");
    }

    [Fact]
    public void ADoorsJointAndToggleAreSavedWithItsPart()
    {
        VehicleFile car = VehicleFileTestsBuilder.WithDoor();

        string json = VehicleFileTestsBuilder.Write(car);
        VehicleFile read = VehicleFileTestsBuilder.Read(json);

        VehiclePart door = read.Doors.Should().ContainSingle().Subject;
        door.Joint.Should().Be(car.Doors[0].Joint);
        door.Toggle.Should().Be(car.Doors[0].Toggle);
        json.Should().Contain("\"joint\"").And.Contain("\"toggle\":\"Interaction.Action.Toggle\"");
    }

    [Fact]
    public void PartsWithoutAJointOrToggleWriteNeither()
    {
        string json = VehicleFileTestsBuilder.Write(VehicleFile.Create(VehicleType.Car));

        json.Should().NotContain("\"joint\"").And.NotContain("\"toggle\"");
    }

    [Fact]
    public void DoorsComeAfterTheWheelsAndKeepTheirIds()
    {
        VehicleFile car = VehicleFileTestsBuilder.WithDoor();
        VehiclePart second = new() { Mesh = SolidMesh.Box(Vector3.One) };
        car.Doors.Add(second);

        car.GetParts().Select(part => part.Id).Should().EndWith(["door.1", "door.2"]);
        car.GetParts()[^1].Part.Should().BeSameAs(second);
    }

    [Fact]
    public void ChangingTheWheelsLeavesTheDoorsAlone()
    {
        VehicleFile car = VehicleFileTestsBuilder.WithDoor();
        VehiclePart door = car.Doors[0];

        car.SetWheelCount(6);
        car.SetType(VehicleType.Motorcycle);

        car.Doors.Should().ContainSingle().Which.Should().BeSameAs(door);
    }
}
