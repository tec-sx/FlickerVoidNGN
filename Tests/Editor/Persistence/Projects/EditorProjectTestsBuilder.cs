using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Persistence.Tests.Projects;

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
        Program = Path.Combine(OutputFolder, "Hollow.Launcher.dll");
        _builder.Build(Arg.Any<string>(), Arg.Any<IProgress<BuildProgress>?>()).Returns(call =>
        {
            _built.Add(call.ArgAt<string>(0));
            Progress = call.ArgAt<IProgress<BuildProgress>?>(1);
            return Program;
        });
    }

    public string Folder { get; }

    public string OutputFolder { get; }

    public string Program { get; }

    public IReadOnlyList<string> Built => _built;

    /// <summary>The progress the last build was given to report to.</summary>
    public IProgress<BuildProgress>? Progress { get; private set; }

    public string WithProjectFile(string name, string json)
    {
        string path = Path.Combine(Folder, name);
        File.WriteAllText(path, json);
        return path;
    }

    public EditorProjectTestsBuilder WithFile(string name)
    {
        string path = Path.Combine(Folder, name);
        File.WriteAllText(path, "");
        return this;
    }

    public EditorProject Open(string file, IProgress<BuildProgress>? progress = null) => EditorProject.Open(file, _builder, progress);

    public void Dispose()
    {
        Directory.Delete(Folder, recursive: true);
    }
}
