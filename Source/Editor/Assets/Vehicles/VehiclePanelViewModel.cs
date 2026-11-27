using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets.Creators;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Assets.Vehicles;

/// <summary>
/// The vehicle creator's panel: the vehicle's type, its wheel count among those the type takes, its parts and how it drives, each choice
/// written to the file through the document; and its look from Blender: Open in Blender, the switch between look and greyboxes, and what
/// the look covers.
/// </summary>
public sealed partial class VehiclePanelViewModel : CreatorPanelViewModel
{
    private readonly VehicleDocument _document;
    private bool _showing;

    private readonly IVehicleLookTools? _looks;

    public VehiclePanelViewModel(VehicleDocument document, IVehicleLookTools? looks = null)
        : base(document)
    {
        _document = document;
        _looks = looks;
        Show();
        RefreshLook();
    }

    public override string Kind => "Vehicle";

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

    public IReadOnlyList<DrivenAxles> DrivenChoices { get; } = Enum.GetValues<DrivenAxles>();

    /// <summary>The vehicle's weight, in kilograms.</summary>
    [ObservableProperty]
    public partial float Mass { get; set; }

    /// <summary>The engine's greatest torque, in newton metres.</summary>
    [ObservableProperty]
    public partial float EngineTorque { get; set; }

    /// <summary>The brakes' greatest torque on each wheel, in newton metres.</summary>
    [ObservableProperty]
    public partial float BrakeTorque { get; set; }

    /// <summary>Which axles the engine turns.</summary>
    [ObservableProperty]
    public partial DrivenAxles Driven { get; set; }

    /// <summary>How far the front wheels turn at full steering, in degrees.</summary>
    [ObservableProperty]
    public partial float Steer { get; set; }

    /// <summary>How far each wheel travels on its suspension, in metres.</summary>
    [ObservableProperty]
    public partial float SuspensionTravel { get; set; }

    /// <summary>How fast the suspension springs back, in cycles a second.</summary>
    [ObservableProperty]
    public partial float SuspensionFrequency { get; set; }

    /// <summary>How quickly the suspension stops bouncing, from 0 to 1.</summary>
    [ObservableProperty]
    public partial float SuspensionDamping { get; set; }

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

    partial void OnMassChanged(float value) => WriteDrive();

    partial void OnEngineTorqueChanged(float value) => WriteDrive();

    partial void OnBrakeTorqueChanged(float value) => WriteDrive();

    partial void OnDrivenChanged(DrivenAxles value) => WriteDrive();

    partial void OnSteerChanged(float value) => WriteDrive();

    partial void OnSuspensionTravelChanged(float value) => WriteDrive();

    partial void OnSuspensionFrequencyChanged(float value) => WriteDrive();

    partial void OnSuspensionDampingChanged(float value) => WriteDrive();

    /// <summary>Writes the drive the panel shows to the file; values a vehicle can't drive with are put back.</summary>
    private void WriteDrive()
    {
        if (_showing)
        {
            return;
        }

        bool drives = Mass > 0 && EngineTorque >= 0 && BrakeTorque >= 0 && Steer is >= 0 and <= 80 && SuspensionTravel > 0
            && SuspensionFrequency > 0 && SuspensionDamping is >= 0 and <= 1;
        if (drives)
        {
            VehicleDrive drive = new()
            {
                Mass = Mass,
                EngineTorque = EngineTorque,
                BrakeTorque = BrakeTorque,
                Driven = Driven,
                Steer = Steer,
                SuspensionTravel = SuspensionTravel,
                SuspensionFrequency = SuspensionFrequency,
                SuspensionDamping = SuspensionDamping,
            };
            _document.SetDrive(drive);
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
        ShowParts();
        VehicleDrive drive = vehicle.GetDrive();
        Mass = drive.Mass;
        EngineTorque = drive.EngineTorque;
        BrakeTorque = drive.BrakeTorque;
        Driven = drive.Driven;
        Steer = drive.Steer;
        SuspensionTravel = drive.SuspensionTravel;
        SuspensionFrequency = drive.SuspensionFrequency;
        SuspensionDamping = drive.SuspensionDamping;
        _showing = false;
    }
}
