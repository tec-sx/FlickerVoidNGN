using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Meshes.Doors;
using FlickerVoid.Engine.Runtime.Solids;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Assets.Vehicles;

/// <summary>
/// The vehicle creator's panel: the vehicle's type, its wheel count among those the type takes, and its parts, each choice written
/// to the file through the document; and its look from Blender: Open in Blender, the switch between look and greyboxes, and what
/// the look covers.
/// </summary>
public sealed partial class VehiclePanelViewModel : ObservableObject
{
    private readonly VehicleDocument _document;
    private bool _showing;

    private readonly IVehicleLookTools? _looks;

    public VehiclePanelViewModel(VehicleDocument document, IVehicleLookTools? looks = null)
    {
        _document = document;
        _looks = looks;
        Show();
        RefreshLook();
    }

    /// <summary>Whether the panel can open the vehicle in Blender and switch its look, which it can in the editor's window.</summary>
    public bool HasLookTools => _looks is not null;

    /// <summary>Whether the vehicle has a look from Blender, so the switch between it and the greyboxes means something.</summary>
    [ObservableProperty]
    public partial bool HasLook { get; private set; }

    /// <summary>What the look covers: how many parts, which keep their greybox and which objects joined the body.</summary>
    [ObservableProperty]
    public partial string LookReport { get; private set; } = string.Empty;

    /// <summary>What Open in Blender last said.</summary>
    [ObservableProperty]
    public partial string BlenderStatus { get; private set; } = string.Empty;

    /// <summary>Whether vehicles are drawn as their looks; off shows the greyboxes under them, as the mesh tools need.</summary>
    public bool ShowsLook
    {
        get => _looks?.ShowGreyboxes == false;
        set
        {
            if (_looks is null || _looks.ShowGreyboxes == !value)
            {
                return;
            }

            _looks.ShowGreyboxes = !value;
            OnPropertyChanged();
        }
    }

    /// <summary>Opens the vehicle in Blender: the first time on its greyboxes, later on its Blender file.</summary>
    [RelayCommand]
    public void OpenInBlender()
    {
        if (_looks is null)
        {
            return;
        }

        BlenderStatus = _looks.OpenInBlender(_document.Path, _document.Vehicle);
    }

    /// <summary>Reads the look again, after the content build baked a new one.</summary>
    public void RefreshLook()
    {
        VehicleLook? look = _looks?.FindLook(_document.Path);
        HasLook = look is not null;
        LookReport = Describe(look);
        OnPropertyChanged(nameof(ShowsLook));
    }

    private string Describe(VehicleLook? look)
    {
        if (look is null)
        {
            return "No look yet. Open in Blender makes one from the greyboxes.";
        }

        string report = $"Look on {look.Parts.Count} of {Parts.Count} parts.";
        if (look.Greyboxed.Count > 0)
        {
            string kept = string.Join(", ", look.Greyboxed);
            report += $" Greybox kept: {kept}.";
        }

        if (look.Unmatched.Count > 0)
        {
            string joined = string.Join(", ", look.Unmatched);
            report += $" Joined the body: {joined}.";
        }

        return report;
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

    /// <summary>The solid of the part with the id as the file holds it, or null when the vehicle has no such part.</summary>
    public SolidMesh? FindMesh(string id)
    {
        foreach ((string partId, VehiclePart part) in _document.Vehicle.GetParts())
        {
            if (partId == id)
            {
                return part.Mesh;
            }
        }

        return null;
    }

    /// <summary>Whether the part with the id has its mirror switch on; false for an id the vehicle lacks.</summary>
    public bool FindMirror(string id)
    {
        foreach ((string partId, VehiclePart part) in _document.Vehicle.GetParts())
        {
            if (partId == id)
            {
                return part.Mirror;
            }
        }

        return false;
    }

    /// <summary>Hangs a door in the part's face on the edge; the new door's id, or null.</summary>
    public string? HangDoor(string partId, int face, SolidEdge hinge, DoorOptions options) => _document.HangDoor(partId, face, hinge, options);

    public void SetMirror(string id, bool mirror) => _document.SetMirror(id, mirror);

    /// <summary>Gives the part a new solid, held in the file as one undo step.</summary>
    public void SetMesh(string id, SolidMesh mesh) => _document.SetMesh(id, mesh);

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
