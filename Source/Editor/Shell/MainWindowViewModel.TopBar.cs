using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Engine.Rendering.Moods;

namespace FlickerVoid.Editor;

/// <summary>The top bar and the bottom strip: the project's name, the level's tab, how much is unsaved and the Day dock's tab.</summary>
public sealed partial class MainWindowViewModel
{
    private const string NoProjectName = "FlickerVoid";

    private const string NoLevelName = "Untitled";

    /// <summary>The project's name on the top bar's menu: its solution's, else the folder holding its content.</summary>
    public string ProjectName
    {
        get
        {
            if (_solution is not null)
            {
                return Path.GetFileNameWithoutExtension(_solution);
            }

            string root = Path.TrimEndingDirectorySeparator(_content.RootDirectory);
            string? project = Path.GetDirectoryName(root);
            if (string.IsNullOrEmpty(project))
            {
                return NoProjectName;
            }

            return Path.GetFileName(project);
        }
    }

    /// <summary>The name on the level's tab: the open scene's file without its folder and extension.</summary>
    public string LevelName
    {
        get
        {
            string? scene = Viewport.Scene.ScenePath;
            if (scene is null)
            {
                return NoLevelName;
            }

            return Path.GetFileNameWithoutExtension(scene);
        }
    }

    /// <summary>Whether the level's tab shows the unsaved dot.</summary>
    public bool IsLevelUnsaved => Viewport.Scene.IsUnsaved;

    /// <summary>How many files hold unsaved edits, the scene included.</summary>
    public int UnsavedCount => UnsavedPaths.Count;

    /// <summary>The top bar's unsaved chip, shown while anything is unsaved.</summary>
    public string UnsavedText => $"{UnsavedCount} unsaved";

    /// <summary>The unsaved files, one a line, for the chip's tip.</summary>
    public string UnsavedTip
    {
        get
        {
            string files = string.Join(Environment.NewLine, UnsavedPaths);
            return $"Unsaved; click to save all (Ctrl+S saves what is shown):{Environment.NewLine}{files}";
        }
    }

    /// <summary>Saves the scene and every asset with unsaved edits: the unsaved chip's click.</summary>
    [RelayCommand]
    public void SaveEverything() => SaveAll();

    /// <summary>The strip's Day tab: closes the Day dock, or opens it on the environment the scene names.</summary>
    [RelayCommand]
    public void ToggleDayDock()
    {
        if (IsDayDockOpen)
        {
            CloseDayDock();
            return;
        }

        string? path = Viewport.EnvironmentPath ?? SceneEnvironment.Find(Viewport.Scene.Game.World);
        if (path is null)
        {
            Viewport.Status = "The scene names no environment: add a Scene Environment, or open an environment file in the content drawer.";
            return;
        }

        OpenEnvironment(path);
    }

    private void RaiseTopBarChanged()
    {
        OnPropertyChanged(nameof(LevelName));
        OnPropertyChanged(nameof(IsLevelUnsaved));
        OnPropertyChanged(nameof(UnsavedCount));
        OnPropertyChanged(nameof(UnsavedText));
        OnPropertyChanged(nameof(UnsavedTip));
    }
}
