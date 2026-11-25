using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Loads the model the content build baked of a source file: Room.glb is read from Room.fvmodel.</summary>
internal sealed class ModelFileMeshSource : IMeshSource
{
    public MadeMesh? Make(string mesh, IContentManager content)
    {
        string builtPath = Path.ChangeExtension(mesh, ".fvmodel");
        string fullPath = content.GetFullPath(builtPath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        ModelData model;
        using (FileStream stream = File.OpenRead(fullPath))
        {
            model = ModelFile.Read(stream);
        }

        return new MadeMesh(model, builtPath);
    }
}
