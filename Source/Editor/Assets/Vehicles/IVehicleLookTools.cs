using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Assets.Vehicles;

/// <summary>What the vehicle creator's panel does with looks: open the vehicle in Blender, read its look and switch looks for greyboxes.</summary>
public interface IVehicleLookTools
{
    /// <summary>Whether vehicles are drawn as their greyboxes, look or not.</summary>
    bool ShowGreyboxes { get; set; }

    /// <summary>Opens the vehicle at the content path in Blender, and says what happened.</summary>
    string OpenInBlender(string path, VehicleFile vehicle);

    /// <summary>The built look of the vehicle at the content path bound to its parts, or null when it has none.</summary>
    VehicleLook? FindLook(string path);
}
