using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Makes the models of mesh names it knows, such as the parts of a creation's file, for the renderer and the editor's picking.</summary>
public interface IMeshSource
{
    /// <summary>The model <paramref name="mesh"/> names and the file it is made from, or null when the name is not one this source makes.</summary>
    MadeMesh? Make(string mesh, IContentManager content);
}
