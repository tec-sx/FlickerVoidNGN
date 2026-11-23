using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;

namespace FlickerVoid.Editor.Tests.Projects;

/// <summary>
/// Builds the project picker over a temporary projects folder, a folder elsewhere and the recent projects file beside them, with a
/// builder and a template that only pretend.
/// </summary>
internal sealed class ProjectPickerViewModelTestsBuilder : IDisposable
{
    private readonly IProjectBuilder _builder = Substitute.For<IProjectBuilder>();
    private readonly IProjectCreator _creator = Substitute.For<IProjectCreator>();
    private readonly List<string> _created = [];
    private readonly string _root;

    public ProjectPickerViewModelTestsBuilder()
    {
        string rootName = Path.GetRandomFileName();
        _root = Path.Combine(Path.GetTempPath(), rootName);
        Folder = Path.Combine(_root, "Projects");
        Directory.CreateDirectory(Folder);
        string recentFile = Path.Combine(_root, "RecentProjects.json");
        Recent = new RecentProjects(recentFile);
        _builder.Build(Arg.Any<string>()).Returns(call => Path.ChangeExtension(call.Arg<string>(), ".dll"));
        _creator.When(creator => creator.Create(Arg.Any<string>(), Arg.Any<string>())).Do(call =>
        {
            string name = call.ArgAt<string>(0);
            _created.Add(name);
            WithProject(name);
        });
        _creator.Create(Arg.Any<string>(), Arg.Any<string>()).Returns(call => ProjectFile(call.ArgAt<string>(0)));
    }

    public string Folder { get; }

    public IReadOnlyList<string> Created => _created;

    public RecentProjects Recent { get; }

    public ProjectPickerViewModelTestsBuilder WithProject(string name)
    {
        string file = ProjectFile(name);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, $$"""{ "game": "Source/Launcher/{{name}}.Launcher.csproj" }""");
        return this;
    }

    /// <summary>Writes a project outside the projects folder, opened in an earlier run, and returns its file.</summary>
    public string WithRememberedProject(string name)
    {
        string folder = Path.Combine(_root, "Elsewhere", name);
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, name + EditorProject.Extension);
        File.WriteAllText(file, $$"""{ "game": "Source/Launcher/{{name}}.Launcher.csproj" }""");
        Recent.Add(file);
        return file;
    }

    /// <summary>Remembers a project of the projects folder as opened in an earlier run.</summary>
    public ProjectPickerViewModelTestsBuilder WithOpenedBefore(string name)
    {
        string file = ProjectFile(name);
        Recent.Add(file);
        return this;
    }

    public ProjectPickerViewModelTestsBuilder WithFailingBuild(string output)
    {
        _builder.Build(Arg.Any<string>()).Returns(_ => throw new ProjectBuildException(output));
        return this;
    }

    public ProjectPickerViewModel Build() => new(Folder, Recent, _builder, _creator);

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private string ProjectFile(string name) => Path.Combine(Folder, name, name + EditorProject.Extension);
}
