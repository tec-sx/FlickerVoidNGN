using System.Text.Json;

using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Persistence.Layout;

/// <summary>
/// How the user arranged the main window's panels, kept in a file of the user's (by default FlickerVoid/Editor/Layout.json in local
/// application data) so the next run opens the same way; a file that can't be read starts over.
/// </summary>
public sealed class EditorLayout
{
    public const double DefaultContentHeight = 318;

    public const double DefaultDayDockHeight = 300;

    public const double DefaultSideWidth = 320;

    /// <summary>The least a remembered panel size can be, so a panel never comes back too small to grab.</summary>
    public const double MinimumSize = 120;

    public const int DefaultAudioBufferFrames = 1024;

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

    /// <summary>Whether the content drawer is open; saved as it changes.</summary>
    public bool ContentOpen
    {
        get => _layout.ContentOpen;
        set
        {
            _layout.ContentOpen = value;
            Save();
        }
    }

    /// <summary>The content drawer's height, never under <see cref="MinimumSize"/>; saved as it changes.</summary>
    public double ContentHeight
    {
        get => Math.Max(_layout.ContentHeight, MinimumSize);
        set
        {
            _layout.ContentHeight = value;
            Save();
        }
    }

    /// <summary>The Day dock's height, never under <see cref="MinimumSize"/>; saved as it changes.</summary>
    public double DayDockHeight
    {
        get => Math.Max(_layout.DayDockHeight, MinimumSize);
        set
        {
            _layout.DayDockHeight = value;
            Save();
        }
    }

    /// <summary>The side column's width, never under <see cref="MinimumSize"/>; saved as it changes.</summary>
    public double SideWidth
    {
        get => Math.Max(_layout.SideWidth, MinimumSize);
        set
        {
            _layout.SideWidth = value;
            Save();
        }
    }

    /// <summary>The frames the music editor's sound device takes at a time; saved as it changes.</summary>
    public int AudioBufferFrames
    {
        get => _layout.AudioBufferFrames;
        set
        {
            _layout.AudioBufferFrames = value;
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
