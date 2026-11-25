using System.Text.Json;

using FlickerVoid.Editor.Assets.Vehicles;
using FlickerVoid.Engine.Vehicles;
using FlickerVoid.Engine.Vehicles.Serialization;

namespace FlickerVoid.Editor.Assets.Tests.Vehicles;

/// <summary>Builds a vehicle document over Van.fvvehicle, a new car, in a temporary content folder with the editor's unsaved content.</summary>
internal sealed class VehicleDocumentTestsBuilder : IDisposable
{
    public const string Van = "Van.fvvehicle";

    private readonly UnsavedContentTestsBuilder _files = new();

    public VehicleDocumentTestsBuilder()
    {
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        car.Schema = ".schemas/vehicle.schema.json";
        string text = JsonSerializer.Serialize(car, VehiclesJsonContext.Default.VehicleFile);
        _files.WithFile(Van, text);
        Content = _files.Build();
    }

    public UnsavedContent Content { get; }

    public VehicleDocument Build() => new(Content, Van);

    /// <summary>The vehicle as every load of the file sees it, such as a placed copy's.</summary>
    public VehicleFile Load() => Content.Load(Van, VehiclesJsonContext.Default.VehicleFile);

    /// <summary>The vehicle the file on disk holds.</summary>
    public VehicleFile ReadDisk()
    {
        string text = _files.ReadFile(Van);
        return JsonSerializer.Deserialize(text, VehiclesJsonContext.Default.VehicleFile)!;
    }

    public void Dispose() => _files.Dispose();
}
