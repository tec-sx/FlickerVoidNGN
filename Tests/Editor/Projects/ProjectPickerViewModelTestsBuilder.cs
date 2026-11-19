using FlickerVoid.Engine.Editor.Projects;

namespace FlickerVoid.Engine.Editor.Tests.Projects;

/// <summary>Builds the project picker over a temporary projects folder, with a builder and a template that only pretend.</summary>
internal sealed class ProjectPickerViewModelTestsBuilder : IDisposable
{
    private readonly IProjectBuilder _builder = Substitute.For<IProjectBuilder>();
    private readonly IProjectCreator _creator = Substitute.For<IProjectCreator>();
    private readonly List<string> _created = [];

    public ProjectPickerViewModelTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        Folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(Folder);
        _builder.Build(Arg.Any<string>()).Returns(call => Path.GetDirectoryName(call.Arg<string>())!);
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

    public ProjectPickerViewModelTestsBuilder WithProject(string name)
    {
        string file = ProjectFile(name);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, $$"""{ "game": "Source/Launcher/{{name}}.Launcher.csproj" }""");
        return this;
    }

    public ProjectPickerViewModelTestsBuilder WithFailingBuild(string output)
    {
        _builder.Build(Arg.Any<string>()).Returns(_ => throw new ProjectBuildException(output));
        return this;
    }

    public ProjectPickerViewModel Build() => new(Folder, _builder, _creator);

    public void Dispose()
    {
        Directory.Delete(Folder, recursive: true);
    }

    private string ProjectFile(string name) => Path.Combine(Folder, name, name + EditorProject.Extension);
}
