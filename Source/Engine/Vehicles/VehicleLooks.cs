using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>
/// The looks of vehicles, a game service: each vehicle file's look is the model the content build baked of the <c>.glb</c> beside it
/// (<c>Van.glb</c> for <c>Van.fvvehicle</c>), bound to the file's parts. The editor's creator turns the looks off to show the
/// greyboxes under them.
/// </summary>
public sealed class VehicleLooks
{
    private readonly CreationFiles<VehicleFile> _files;
    private bool _showGreyboxes;

    internal VehicleLooks(CreationFiles<VehicleFile> files)
    {
        _files = files;
    }

    /// <summary>Whether every vehicle is drawn as its greyboxes, look or not; the game never sets it.</summary>
    public bool ShowGreyboxes
    {
        get => _showGreyboxes;
        set
        {
            if (_showGreyboxes == value)
            {
                return;
            }

            _showGreyboxes = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Raised when <see cref="ShowGreyboxes"/> changes.</summary>
    public event Action? Changed;

    /// <summary>The look of the vehicle file bound to its parts, or null when the file can't be read or no look was built for it.</summary>
    public VehicleLook? Find(IContentManager content, string vehiclePath)
    {
        string builtPath = VehicleLook.GetBuiltPath(vehiclePath);
        string fullPath = content.GetFullPath(builtPath);
        if (!File.Exists(fullPath) || _files.Read(content, vehiclePath) is not { } vehicle)
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(fullPath);
            ModelData model = ModelFile.Read(stream);
            return VehicleLook.Bind(vehicle, model);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return null;
        }
    }
}
