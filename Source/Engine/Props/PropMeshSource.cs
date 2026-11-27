using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Props;

/// <summary>Makes the meshes of props' parts from their files: <c>Crate.fvprop#part.1</c> is that part's solid.</summary>
internal sealed class PropMeshSource(CreationFiles<PropFile> files) : IMeshSource
{
    private const char Separator = '#';

    public static string GetName(string file, string id) => $"{file}{Separator}{id}";

    public MadeMesh? Make(string mesh, IContentManager content)
    {
        string[] names = mesh.Split(Separator);
        if (names.Length != 2 || !names[0].EndsWith(PropFile.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        PropFile? file = files.Read(content, names[0]);
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
