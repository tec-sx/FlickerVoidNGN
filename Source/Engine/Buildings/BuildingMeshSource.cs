using System.Numerics;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Buildings;

/// <summary>Makes the meshes of buildings' parts from their files: <c>Inn.fvbuilding#storey.0</c> is that storey's solid.</summary>
internal sealed class BuildingMeshSource(CreationFiles<BuildingFile> files) : IMeshSource
{
    private const char Separator = '#';
    private const string FloorSuffix = ".floor";

    /// <summary>A floor's grey, darker than the walls', so a storey reads from above, where a wall's top and the floor face the same way.</summary>
    private static readonly Vector4 FloorColor = new(0.42f, 0.42f, 0.42f, 1);

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
                bool floor = id.EndsWith(FloorSuffix, StringComparison.Ordinal);
                ModelData solid = SolidModel.Create(part.Mesh, floor ? FloorColor : SolidModel.Color);
                return new MadeMesh(solid, names[0]);
            }
        }

        return null;
    }
}
