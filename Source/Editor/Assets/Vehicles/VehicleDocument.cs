using FlickerVoid.Editor.Assets.Creators;
using FlickerVoid.Engine.Vehicles;
using FlickerVoid.Engine.Vehicles.Serialization;

namespace FlickerVoid.Editor.Assets.Vehicles;

/// <summary>
/// A vehicle file being edited in the creator: each change is held unsaved, one undo step in the file's history, and reaches every
/// placed copy through the change hot reload reports.
/// </summary>
public sealed class VehicleDocument(UnsavedContent content, string path)
    : CreationDocument<VehicleFile>(content, path, VehiclesJsonContext.Default.VehicleFile)
{
    public VehicleFile Vehicle => Value;

    /// <summary>Makes it a car or a motorcycle, with the wheel count nearest its own that the type takes.</summary>
    public void SetType(VehicleType type)
    {
        Vehicle.SetType(type);
        Hold();
    }

    /// <summary>Adds or removes axles at the back; throws for a count the type doesn't take.</summary>
    public void SetWheelCount(int count)
    {
        Vehicle.SetWheelCount(count);
        Hold();
    }

    /// <summary>Sets how the vehicle drives, held in the file as one undo step.</summary>
    public void SetDrive(VehicleDrive drive)
    {
        Vehicle.Drive = drive;
        Hold();
    }
}
