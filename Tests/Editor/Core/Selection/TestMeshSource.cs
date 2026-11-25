using System.Globalization;
using System.Numerics;

using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Selection;

/// <summary>Makes the parts of ".fvtest" files, as a creations module would: "Box.fvtest#body" is a box as big each way as the number the file holds.</summary>
internal sealed class TestMeshSource : IMeshSource
{
    private const string Extension = ".fvtest";

    public MadeMesh? Make(string mesh, IContentManager content)
    {
        int separator = mesh.IndexOf('#', StringComparison.Ordinal);
        if (separator < 0)
        {
            return null;
        }

        string file = mesh[..separator];
        if (!file.EndsWith(Extension, StringComparison.Ordinal))
        {
            return null;
        }

        string fullPath = content.GetFullPath(file);
        string text = File.ReadAllText(fullPath);
        float size = float.Parse(text, CultureInfo.InvariantCulture);
        SolidMesh box = SolidMesh.Box(new Vector3(size));
        ModelData model = SolidModel.Create(box);
        return new MadeMesh(model, file);
    }
}
