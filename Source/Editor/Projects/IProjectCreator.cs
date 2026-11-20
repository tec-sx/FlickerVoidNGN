namespace FlickerVoid.Engine.Editor.Projects;

/// <summary>Creates new projects.</summary>
public interface IProjectCreator
{
    /// <summary>Creates the project <paramref name="name"/> in a folder of that name in <paramref name="folder"/> and returns its project file.</summary>
    string Create(string name, string folder);
}
