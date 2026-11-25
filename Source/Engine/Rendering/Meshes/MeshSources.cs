using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Turns mesh names into models for the renderer and the editor's picking: greybox boxes first, then each source a module added, in
/// order, and last the model the content build baked of a source file (Room.glb is drawn from Room.fvmodel). A game service from
/// <c>AddRendering</c>.
/// </summary>
public sealed class MeshSources
{
    private readonly List<IMeshSource> _sources = [new GreyboxMeshSource()];
    private readonly ModelFileMeshSource _files = new();

    /// <summary>Asks <paramref name="source"/> after the sources added before it and before the model files.</summary>
    public void Add(IMeshSource source)
    {
        _sources.Add(source);
    }

    /// <summary>The model the mesh names and the file it was made from; null when no source makes it and its model file is missing.</summary>
    public MadeMesh? Make(string mesh, IContentManager content)
    {
        foreach (IMeshSource source in _sources)
        {
            MadeMesh? made = source.Make(mesh, content);
            if (made is not null)
            {
                return made;
            }
        }

        return _files.Make(mesh, content);
    }
}
