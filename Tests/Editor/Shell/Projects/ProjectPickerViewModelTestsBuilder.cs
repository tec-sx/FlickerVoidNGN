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
        _builder.Build(Arg.Any<string>(), Arg.Any<IProgress<BuildProgress>?>()).Returns(call => Path.ChangeExtension(call.ArgAt<string>(0), ".dll"));
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

    public ProjectPickerViewModelTestsBuilder WithProject(string name, string? splash = null)
    {
        string file = ProjectFile(name);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        string splashProperty = "";
        if (splash is not null)
        {
            splashProperty = $$""", "splash": "{{splash}}" """;
        }

        File.WriteAllText(file, $$"""{ "game": "Source/Launcher/{{name}}.Launcher.csproj"{{splashProperty}} }""");
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

    public ProjectPickerViewModelTestsBuilder WithFailingBuild(string output) => WithBuildThrowing(new ProjectBuildException(output));

    public ProjectPickerViewModelTestsBuilder WithBuildThrowing(Exception exception)
    {
        _builder.Build(Arg.Any<string>(), Arg.Any<IProgress<BuildProgress>?>()).Returns(_ => throw exception);
        return this;
    }

    /// <summary>Makes the build report <paramref name="progress"/> before it succeeds.</summary>
    public ProjectPickerViewModelTestsBuilder WithBuildReporting(BuildProgress progress)
    {
        _builder.Build(Arg.Any<string>(), Arg.Any<IProgress<BuildProgress>?>()).Returns(call =>
        {
            IProgress<BuildProgress>? reported = call.ArgAt<IProgress<BuildProgress>?>(1);
            reported?.Report(progress);
            return Path.ChangeExtension(call.ArgAt<string>(0), ".dll");
        });
        return this;
    }

    /// <summary>Makes the build wait until <paramref name="finish"/> is set, as a real one takes a while.</summary>
    public ProjectPickerViewModelTestsBuilder WithBuildWaitingFor(ManualResetEventSlim finish)
    {
        _builder.Build(Arg.Any<string>(), Arg.Any<IProgress<BuildProgress>?>()).Returns(call =>
        {
            finish.Wait(TimeSpan.FromSeconds(30));
            return Path.ChangeExtension(call.ArgAt<string>(0), ".dll");
        });
        return this;
    }

    /// <summary>The status the next splash the picker shows changes to, once it does: reports reach it on the thread that opened it.</summary>
    public Task<string> NextSplashStatus(ProjectPickerViewModel picker)
    {
        TaskCompletionSource<string> status = new(TaskCreationOptions.RunContinuationsAsynchronously);
        picker.Opening += splash => splash.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SplashViewModel.Status))
            {
                status.TrySetResult(splash.Status);
            }
        };
        return status.Task;
    }

    public ProjectPickerViewModel Build() => new(Folder, Recent, _builder, _creator);

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    public string ProjectFile(string name) => Path.Combine(Folder, name, name + EditorProject.Extension);
}
