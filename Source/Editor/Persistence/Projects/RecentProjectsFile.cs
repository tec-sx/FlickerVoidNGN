namespace FlickerVoid.Editor.Persistence.Projects;

/// <summary>The recent projects file: the project files the editor opened, newest first.</summary>
public sealed class RecentProjectsFile
{
    public List<string> Projects { get; set; } = [];
}
