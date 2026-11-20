using System.Text.Json;

namespace FlickerVoid.Editor.Persistence.Projects;

/// <summary>
/// A project opened from its <c>.fvproject</c> file: the game is built first, which builds the content and writes the build
/// manifest beside it, so the editor loads the built content while its saves and the builder in watch mode follow the source.
/// </summary>
public sealed class EditorProject
{
    public const string Extension = ".fvproject";

    private EditorProject(string file, string game, string program, string? startupScene)
    {
        File = file;
        Game = game;
        Program = program;
        string output = Path.GetDirectoryName(program)!;
        ContentFolder = Path.Combine(output, "Content");
        StartupScene = startupScene;
    }

    /// <summary>The project file's full path.</summary>
    public string File { get; }

    public string Name => Path.GetFileNameWithoutExtension(File);

    public string Folder => Path.GetDirectoryName(File)!;

    /// <summary>The game's C# project, as a full path.</summary>
    public string Game { get; }

    /// <summary>The built game, which Play runs.</summary>
    public string Program { get; }

    /// <summary>The built content folder in the game's output folder.</summary>
    public string ContentFolder { get; }

    public string? StartupScene { get; }

    /// <summary>Reads the project file and builds its game; throws <see cref="InvalidDataException"/> for a bad file and <see cref="ProjectBuildException"/> for a failed build.</summary>
    public static EditorProject Open(string file, IProjectBuilder builder)
    {
        string fullPath = Path.GetFullPath(file);
        ProjectFile project = Read(fullPath);
        if (string.IsNullOrEmpty(project.Game))
        {
            throw new InvalidDataException($"{fullPath} names no game: add \"game\" with the game's C# project.");
        }

        string folder = Path.GetDirectoryName(fullPath)!;
        string game = Path.GetFullPath(project.Game, folder);
        string program = builder.Build(game);
        return new EditorProject(fullPath, game, program, project.StartupScene);
    }

    private static ProjectFile Read(string fullPath)
    {
        try
        {
            string json = System.IO.File.ReadAllText(fullPath);
            ProjectFile? project = JsonSerializer.Deserialize(json, EditorJsonContext.Default.ProjectFile);
            return project ?? throw new InvalidDataException($"{fullPath} is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{fullPath} can't be read: {exception.Message}", exception);
        }
    }
}
