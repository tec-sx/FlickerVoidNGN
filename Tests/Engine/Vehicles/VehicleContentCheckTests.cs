using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Vehicles.Tests;

public sealed class VehicleContentCheckTests : IDisposable
{
    private readonly VehicleContentCheckTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ReportsAVehicleWhoseMeshIsOpen()
    {
        string json = VehicleFileTestsBuilder.WithOpenBody();
        _builder.WithFile("Vehicles/Van.fvvehicle", json);

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().ContainSingle();
        problems[0].Path.Should().Be("Vehicles/Van.fvvehicle");
        problems[0].Message.Should().Contain("The solid is open");
    }

    [Fact]
    public void PassesANewCar()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        string json = VehicleFileTestsBuilder.Write(car);
        _builder.WithFile("Vehicles/Van.fvvehicle", json);

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().BeEmpty();
    }
}
