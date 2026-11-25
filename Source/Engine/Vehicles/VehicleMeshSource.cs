using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>Makes the meshes of vehicles' parts from their files: <c>Vans/Van.fvvehicle#wheel.1L</c> is that wheel's solid.</summary>
internal sealed class VehicleMeshSource(VehicleFiles files) : IMeshSource
{
    private const char Separator = '#';

    /// <summary>The mesh name of the part with the id in the vehicle file.</summary>
    public static string GetName(string file, string id) => $"{file}{Separator}{id}";

    public MadeMesh? Make(string mesh, IContentManager content)
    {
        int separator = mesh.LastIndexOf(Separator);
        if (separator < 0)
        {
            return null;
        }

        string path = mesh[..separator];
        if (!path.EndsWith(VehicleFile.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        VehicleFile? file = files.Read(content, path);
        if (file is null)
        {
            return null;
        }

        string id = mesh[(separator + 1)..];
        foreach ((string partId, VehiclePart part) in file.GetParts())
        {
            if (partId == id)
            {
                ModelData model = SolidModel.Create(part.Mesh);
                return new MadeMesh(model, path);
            }
        }

        return null;
    }
}
