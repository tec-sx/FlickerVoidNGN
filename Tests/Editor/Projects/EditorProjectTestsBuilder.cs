using FlickerVoid.Engine.Editor.Projects;

namespace FlickerVoid.Engine.Editor.Tests.Projects;

/// <summary>Opens projects in a temporary folder with a builder that only records what it was asked to build.</summary>
internal sealed class EditorProjectTestsBuilder : IDisposable
{
    private readonly List<string> _built = [];
    private readonly IProjectBuilder _builder = Substitute.For<IProjectBuilder>();

    public EditorProjectTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        Folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(Folder);
        OutputFolder = Path.Combine(Folder, "bin");
        _builder.Build(Arg.Any<string>()).Returns(call =>
        {
            _built.Add(call.Arg<string>());
            return OutputFolder;
        });
    }

    public string Folder { get; }

    public string OutputFolder { get; }

    public IReadOnlyList<string> Built => _built;

    public string WithProjectFile(string name, string json)
    {
        string path = Path.Combine(Folder, name);
        File.WriteAllText(path, json);
        return path;
    }

    public EditorProject Open(string file) => EditorProject.Open(file, _builder);

    public void Dispose()
    {
        Directory.Delete(Folder, recursive: true);
    }
}
