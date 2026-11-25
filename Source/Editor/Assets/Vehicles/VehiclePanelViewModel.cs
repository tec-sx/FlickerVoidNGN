using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Assets.Vehicles;

/// <summary>
/// The vehicle creator's panel: the vehicle's type, its wheel count among those the type takes, and its parts, each choice written
/// to the file through the document.
/// </summary>
public sealed partial class VehiclePanelViewModel : ObservableObject
{
    private readonly VehicleDocument _document;
    private bool _showing;

    public VehiclePanelViewModel(VehicleDocument document)
    {
        _document = document;
        Show();
    }

    public IReadOnlyList<VehicleType> Types { get; } = Enum.GetValues<VehicleType>();

    [ObservableProperty]
    public partial VehicleType Type { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<int> WheelCounts { get; private set; } = [];

    [ObservableProperty]
    public partial int WheelCount { get; set; }

    /// <summary>The ids of the vehicle's parts: its body, then its wheels from the front.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Parts { get; private set; } = [];

    partial void OnTypeChanged(VehicleType value)
    {
        if (!_showing && value != _document.Vehicle.Type)
        {
            _document.SetType(value);
        }

        Show();
    }

    // A count the type doesn't take, such as one typed in, is put back.
    partial void OnWheelCountChanged(int value)
    {
        if (!_showing && WheelCounts.Contains(value) && value != _document.Vehicle.Wheels.Count)
        {
            _document.SetWheelCount(value);
        }

        Show();
    }

    private void Show()
    {
        if (_showing)
        {
            return;
        }

        _showing = true;
        VehicleFile vehicle = _document.Vehicle;
        Type = vehicle.Type;
        WheelCounts = VehicleFile.GetWheelCounts(vehicle.Type);
        WheelCount = vehicle.Wheels.Count;
        IReadOnlyList<(string Id, VehiclePart Part)> parts = vehicle.GetParts();
        Parts = [.. parts.Select(static part => part.Id)];
        _showing = false;
    }
}
