using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Buildings;

/// <summary>Makes the meshes of buildings' parts from their files: <c>Inn.fvbuilding#storey.0</c> is that storey's solid.</summary>
internal sealed class BuildingMeshSource(CreationFiles<BuildingFile> files) : IMeshSource
{
    private const char Separator = '#';

    public static string GetName(string file, string id) => $"{file}{Separator}{id}";

    public MadeMesh? Make(string mesh, IContentManager content)
    {
        string[] names = mesh.Split(Separator);
        if (names.Length != 2 || !names[0].EndsWith(BuildingFile.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        BuildingFile? file = files.Read(content, names[0]);
        if (file is null)
        {
            return null;
        }

        foreach ((string id, SolidPart part) in file.GetParts())
        {
            if (id == names[1])
            {
                ModelData solid = SolidModel.Create(part.Mesh);
                return new MadeMesh(solid, names[0]);
            }
        }

        return null;
    }
}
