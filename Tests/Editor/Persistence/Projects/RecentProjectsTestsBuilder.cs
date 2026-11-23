using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Persistence.Tests.Projects;

/// <summary>Builds the recent projects list over a temporary folder holding its file and the projects it remembers.</summary>
internal sealed class RecentProjectsTestsBuilder : IDisposable
{
    public RecentProjectsTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        Folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(Folder);
    }

    public string Folder { get; }

    public string File => Path.Combine(Folder, "Settings", "RecentProjects.json");

    /// <summary>Writes a project file in a folder of its own and returns its path.</summary>
    public string WithProject(string name)
    {
        string folder = Path.Combine(Folder, name);
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, name + EditorProject.Extension);
        System.IO.File.WriteAllText(file, $$"""{ "game": "Source/Launcher/{{name}}.Launcher.csproj" }""");
        return file;
    }

    public RecentProjectsTestsBuilder WithBrokenFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, "{ not json");
        return this;
    }

    public RecentProjects Build() => new(File);

    public void Dispose()
    {
        Directory.Delete(Folder, recursive: true);
    }
}
