using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Projects;

/// <summary>
/// The window the editor opens with when it is given no project: the projects in the projects folder (Documents/FlickerVoid) and
/// those opened before from anywhere (<see cref="RecentProjects"/>), with the last one selected, another project file, or a new
/// project from the template. Opening shows a splash (<see cref="Opening"/>), builds the project away from the UI thread,
/// remembers it and raises <see cref="Opened"/>, or <see cref="Failed"/> with the exception's log.
/// </summary>
public sealed partial class ProjectPickerViewModel : ObservableObject
{
    private readonly IProjectBuilder _builder;
    private readonly IProjectCreator _creator;
    private readonly RecentProjects _recent;

    public ProjectPickerViewModel(string projectsFolder, RecentProjects recent, IProjectBuilder builder, IProjectCreator creator)
    {
        ProjectsFolder = projectsFolder;
        _recent = recent;
        _builder = builder;
        _creator = creator;
        IReadOnlyList<string> recentFiles = recent.Files;
        Projects = FindProjects(projectsFolder, recentFiles);
        if (recentFiles.Count > 0)
        {
            SelectedProject = Projects.FirstOrDefault(project => project.File == recentFiles[0]);
        }
    }

    /// <summary>Raised as a project starts opening, with the splash to show until it opens or fails.</summary>
    public event Action<SplashViewModel>? Opening;

    public event Action<EditorProject>? Opened;

    /// <summary>Raised when a project fails to open, with the exception's log to show.</summary>
    public event Action<ProjectOpenFailure>? Failed;

    public string ProjectsFolder { get; }

    public IReadOnlyList<ProjectEntry> Projects { get; }

    [ObservableProperty]
    public partial ProjectEntry? SelectedProject { get; set; }

    /// <summary>The name of the project to create: a C# identifier, since it names the game's namespaces.</summary>
    [ObservableProperty]
    public partial string NewName { get; set; } = "";

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>The projects folder of the signed-in user: Documents/FlickerVoid.</summary>
    public static string GetDefaultProjectsFolder()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, "FlickerVoid");
    }

    [RelayCommand]
    public Task Open(ProjectEntry? project)
    {
        if (project is null)
        {
            return Task.CompletedTask;
        }

        return OpenFile(project.File);
    }

    /// <summary>Builds and opens the project file behind its splash.</summary>
    public async Task OpenFile(string file)
    {
        string name = Path.GetFileNameWithoutExtension(file);
        await Run(name, file, $"Opening {name}", progress => OpenProject(file, progress));
    }

    [RelayCommand]
    public async Task Create()
    {
        string name = NewName.Trim();
        if (!IsValidName(name))
        {
            Status = $"'{name}' can't name a project: use letters and digits, starting with a letter.";
            return;
        }

        await Run(name, null, $"Creating {name}", progress =>
        {
            Directory.CreateDirectory(ProjectsFolder);
            string file = _creator.Create(name, ProjectsFolder);
            return OpenProject(file, progress);
        });
    }

    private EditorProject OpenProject(string file, IProgress<BuildProgress> progress)
    {
        EditorProject project = EditorProject.Open(file, _builder, progress);
        _recent.Add(file);
        return project;
    }

    /// <summary>
    /// Shows the splash of the project (its file's image, none for one still to create) and opens it away from the UI thread,
    /// the splash following the build. Whatever is thrown goes to <see cref="Failed"/> rather than ending the editor, as opening
    /// runs the template, the project's build and its files.
    /// </summary>
    private async Task Run(string name, string? file, string doing, Func<IProgress<BuildProgress>, EditorProject> open)
    {
        IsBusy = true;
        Status = "";
        EditorProject? project = null;
        try
        {
            string? image = null;
            if (file is not null)
            {
                image = EditorProject.ReadSplash(file);
            }

            SplashViewModel splash = new(name, image, doing);
            Opening?.Invoke(splash);
            Progress<BuildProgress> progress = new(splash.Show);
            project = await Task.Run(() => open(progress));
        }
        catch (Exception exception)
        {
            Status = $"{name} didn't open.";
            string log = exception.ToString();
            Failed?.Invoke(new ProjectOpenFailure(name, log));
        }
        finally
        {
            IsBusy = false;
        }

        if (project is not null)
        {
            Opened?.Invoke(project);
        }
    }

    private static bool IsValidName(string name)
    {
        if (name.Length == 0 || !char.IsAsciiLetter(name[0]))
        {
            return false;
        }

        foreach (char character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static List<ProjectEntry> FindProjects(string folder, IReadOnlyList<string> recentFiles)
    {
        List<string> files = [];
        if (Directory.Exists(folder))
        {
            foreach (string projectFolder in Directory.EnumerateDirectories(folder))
            {
                IEnumerable<string> found = Directory.EnumerateFiles(projectFolder, "*" + EditorProject.Extension);
                files.AddRange(found.Select(Path.GetFullPath));
            }
        }

        files.AddRange(recentFiles.Except(files));
        List<ProjectEntry> projects = [];
        foreach (string file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            projects.Add(new ProjectEntry(name, file));
        }

        projects.Sort(static (first, second) => StringComparer.OrdinalIgnoreCase.Compare(first.Name, second.Name));
        return projects;
    }
}
