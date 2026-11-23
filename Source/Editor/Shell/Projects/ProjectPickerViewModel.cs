using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Projects;

/// <summary>
/// The window the editor opens with when it is given no project: the projects in the projects folder (Documents/FlickerVoid) and
/// those opened before from anywhere (<see cref="RecentProjects"/>), with the last one selected, another project file, or a new
/// project from the template. Opening builds the project away from the UI thread, remembers it and raises <see cref="Opened"/>.
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

    public event Action<EditorProject>? Opened;

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

    /// <summary>Builds and opens the project file, showing what failed in <see cref="Status"/>.</summary>
    public async Task OpenFile(string file)
    {
        await Run($"Building {Path.GetFileNameWithoutExtension(file)}...", () => OpenProject(file));
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

        await Run($"Creating {name}...", () =>
        {
            Directory.CreateDirectory(ProjectsFolder);
            string file = _creator.Create(name, ProjectsFolder);
            return OpenProject(file);
        });
    }

    private EditorProject OpenProject(string file)
    {
        EditorProject project = EditorProject.Open(file, _builder);
        _recent.Add(file);
        return project;
    }

    private async Task Run(string doing, Func<EditorProject> work)
    {
        IsBusy = true;
        Status = doing;
        try
        {
            EditorProject project = await Task.Run(work);
            Status = "";
            Opened?.Invoke(project);
        }
        catch (Exception exception) when (exception is ProjectBuildException or InvalidDataException or IOException)
        {
            Status = exception.Message;
        }
        finally
        {
            IsBusy = false;
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
