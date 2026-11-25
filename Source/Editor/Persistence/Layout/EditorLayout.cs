using System.Text.Json;

using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Persistence.Layout;

/// <summary>
/// How the user arranged the main window's panels, kept in a file of the user's (by default FlickerVoid/Editor/Layout.json in local
/// application data) so the next run opens the same way; a file that can't be read starts over.
/// </summary>
public sealed class EditorLayout
{
    private readonly string _file;
    private readonly EditorLayoutFile _layout;

    public EditorLayout(string file)
    {
        _file = file;
        _layout = Read(file);
    }

    /// <summary>Whether the outliner is over the context panel instead of under it; saved as it changes.</summary>
    public bool PanelsSwapped
    {
        get => _layout.PanelsSwapped;
        set
        {
            _layout.PanelsSwapped = value;
            Save();
        }
    }

    public static string GetDefaultFile()
    {
        string data = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(data, "FlickerVoid", "Editor", "Layout.json");
    }

    private void Save()
    {
        string json = JsonSerializer.Serialize(_layout, EditorJsonContext.Default.EditorLayoutFile);
        string folder = Path.GetDirectoryName(_file)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(_file, json);
    }

    private static EditorLayoutFile Read(string file)
    {
        if (!File.Exists(file))
        {
            return new EditorLayoutFile();
        }

        try
        {
            string json = File.ReadAllText(file);
            EditorLayoutFile? read = JsonSerializer.Deserialize(json, EditorJsonContext.Default.EditorLayoutFile);
            return read ?? new EditorLayoutFile();
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            return new EditorLayoutFile();
        }
    }
}
