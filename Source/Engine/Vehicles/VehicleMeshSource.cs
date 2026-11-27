using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>
/// Makes the meshes of vehicles' parts from their files: <c>Vans/Van.fvvehicle#wheel.1L</c> is that wheel's look when the vehicle
/// has one with an object for it, else its solid, and <c>Vans/Van.fvvehicle#wheel.1L#greybox</c> is always its solid.
/// </summary>
internal sealed class VehicleMeshSource(CreationFiles<VehicleFile> files, VehicleLooks looks) : IMeshSource
{
    private const char Separator = '#';
    private const string Greybox = "greybox";

    /// <summary>The mesh name of the part with the id in the vehicle file: its look if it has one, or always its greybox.</summary>
    public static string GetName(string file, string id, bool greybox = false)
    {
        if (greybox)
        {
            return $"{file}{Separator}{id}{Separator}{Greybox}";
        }

        return $"{file}{Separator}{id}";
    }

    public MadeMesh? Make(string mesh, IContentManager content)
    {
        string[] names = mesh.Split(Separator);
        if (names.Length < 2 || names.Length > 3 || !names[0].EndsWith(VehicleFile.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string path = names[0];
        string id = names[1];
        bool greybox = names.Length == 3;
        if (greybox && names[2] != Greybox)
        {
            return null;
        }

        VehicleFile? file = files.Read(content, path);
        if (file is null)
        {
            return null;
        }

        foreach ((string partId, SolidPart part) in file.GetParts())
        {
            if (partId != id)
            {
                continue;
            }

            if (greybox)
            {
                ModelData solid = SolidModel.Create(part.Mesh);
                return new MadeMesh(solid, path);
            }

            string builtLook = VehicleLook.GetBuiltPath(path);
            ModelData? model = looks.Find(content, path)?.Parts.GetValueOrDefault(id);
            model ??= SolidModel.Create(part.Mesh);
            return new MadeMesh(model, path) { OtherFiles = [builtLook] };
        }

        return null;
    }
}
