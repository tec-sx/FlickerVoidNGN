using System.Text.Json;

namespace FlickerVoid.Editor.Persistence.Projects;

/// <summary>
/// The project files the editor opened, newest first and at most <see cref="Limit"/>, kept in a file of the user's (by default
/// FlickerVoid/Editor/RecentProjects.json in local application data) so the project picker lists them in the next run wherever they are.
/// </summary>
public sealed class RecentProjects
{
    public const int Limit = 10;

    private readonly string _file;
    private readonly List<string> _files;

    public RecentProjects(string file)
    {
        _file = file;
        _files = Read(file);
    }

    /// <summary>The remembered project files that still exist, newest first.</summary>
    public IReadOnlyList<string> Files => _files.Where(File.Exists).ToList();

    public static string GetDefaultFile()
    {
        string data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(data, "FlickerVoid", "Editor", "RecentProjects.json");
    }

    /// <summary>Puts the project file first and saves the list, forgetting files that are gone and the oldest past the limit.</summary>
    public void Add(string file)
    {
        string full = Path.GetFullPath(file);
        _files.RemoveAll(known => known == full || !File.Exists(known));
        _files.Insert(0, full);
        if (_files.Count > Limit)
        {
            _files.RemoveRange(Limit, _files.Count - Limit);
        }

        RecentProjectsFile saved = new() { Projects = [.. _files] };
        string json = JsonSerializer.Serialize(saved, EditorJsonContext.Default.RecentProjectsFile);
        string folder = Path.GetDirectoryName(_file)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(_file, json);
    }

    private static List<string> Read(string file)
    {
        if (!File.Exists(file))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(file);
            RecentProjectsFile? read = JsonSerializer.Deserialize(json, EditorJsonContext.Default.RecentProjectsFile);
            return read?.Projects ?? [];
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            return [];
        }
    }
}
