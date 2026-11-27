using FlickerVoid.Editor.Assets.Blender;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Assets.Vehicles;

/// <summary>The look tools over the editor's content, the Blender round trip and the scene's <see cref="VehicleLooks"/>.</summary>
public sealed class VehicleLookTools(IContentManager content, BlenderRoundTrip blender, VehicleLooks looks) : IVehicleLookTools
{
    public bool ShowGreyboxes
    {
        get => looks.ShowGreyboxes;
        set => looks.ShowGreyboxes = value;
    }

    public string OpenInBlender(string path, VehicleFile vehicle) => blender.Open(path, vehicle);

    public VehicleLook? FindLook(string path) => looks.Find(content, path);
}
