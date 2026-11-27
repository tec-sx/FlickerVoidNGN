using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Assets.Moods;
using FlickerVoid.Editor.Assets.Textures;
using FlickerVoid.Editor.Core.Flows;
using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Editor.Documents;
using FlickerVoid.Editor.Persistence.Layout;
using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;
using FlickerVoid.Editor.Viewports;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Content;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

namespace FlickerVoid.Editor;

/// <summary>
/// The editor's main window: its title, the scene view, the asset browser, the inspector, the view settings and Play. The
/// viewport draws with the mood file the scene names (<see cref="SceneMood"/>) or the one opened last, else the default
/// mood; the View tab edits that file, holding the edits unsaved with the materials'.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    public const string DefaultMoodHeading = "Default mood (not saved)";

    private readonly ScenePlayer? _player;

    private readonly GameCompiler? _compiler;

    private readonly string? _solution;

    private readonly UnsavedContent _content;

    private MoodDocument? _mood;

    private string? _dataPath;

    private UndoHistory? _watchedHistory;

    private string? _sceneMood;

    private readonly HashSet<string> _openFlows = new(ContentManager.PathComparer);
    private readonly HashSet<string> _openMusic = new(ContentManager.PathComparer);
    private readonly Dictionary<string, Core.Environments.EnvironmentEditorViewModel> _openEnvironments = new(ContentManager.PathComparer);
    private Dictionary<AssetType, Action<AssetEntry>>? _openers;

    /// <summary>
    /// Shows the scene over <paramref name="content"/>, the scene's content with its unsaved edits; <paramref name="player"/>
    /// plays it in the project's game, when a project is open, and the asset browser shows <paramref name="codeFolder"/>, the
    /// project's source folder; <paramref name="compiler"/> builds the project's code again and reloads it, and
    /// <paramref name="solution"/> is the project's solution, opened in Visual Studio; <paramref name="layout"/> remembers how the panels
    /// are arranged.
    /// </summary>
    public MainWindowViewModel(
        ViewportViewModel viewport,
        InspectorViewModel inspector,
        UnsavedContent content,
        ScenePlayer? player = null,
        string? codeFolder = null,
        GameCompiler? compiler = null,
        string? solution = null,
        EditorLayout? layout = null)
    {
        _layout = layout;
        RestoreLayout();
        _player = player;
        _compiler = compiler;
        _solution = solution;
        _content = content;
        _content.UnsavedChanged += HandleUnsavedChanged;
        _content.ChangedOnDisk += HandleChangedOnDisk;
        _content.Changed += HandleContentChanged;
        _content.Restored += HandleRestored;
        Viewport = viewport;
        Inspector = inspector;
        SceneTree = new SceneTreeViewModel(viewport.Scene);
        EntityTree = new EntityTreeViewModel(viewport.Scene);
        ContentFileFinder files = new(content);
        ViewSettings = new ViewSettingsViewModel(viewport.Mood, files.Find);
        Assets = new AssetBrowserViewModel(viewport.Scene.Content, viewport.Scene.Game.Data, codeFolder);
        Assets.Opened += OpenAsset;
        viewport.Scene.Opened += HandleSceneOpened;
        viewport.Scene.UnsavedChanged += HandleUnsavedChanged;
        viewport.Scene.Edited += HandleSceneEdited;
        viewport.CreationRequested += EditCreation;
        viewport.CreationFileRequested += OpenCreation;
        viewport.PieceRequested += ShapePiece;
        viewport.PropertyChanged += HandleViewportChanged;
        viewport.Selection.Changed += HandleSelectionChanged;
        viewport.MeshTools.PropertyChanged += HandleMeshToolsChanged;
        ShowSceneMood();
        WatchHistory();
    }

    /// <summary>Raised to show a flow graph opened in the content browser in a graph editor window.</summary>
    public event Action<string, FlowGraphViewModel>? FlowRequested;

    public event Action<string, MusicEditorViewModel>? MusicRequested;

    /// <summary>Asks the window to show an environment in its editor window, the one it has already when it has one.</summary>
    public event Action<string, Core.Environments.EnvironmentEditorViewModel>? EnvironmentRequested;

    /// <summary>Raised to show a material in the material editor, by its path in the content folder.</summary>
    public event Action<string>? MaterialRequested;

    /// <summary>Raised to show an image's or a canvas's settings in a texture editor window.</summary>
    public event Action<TextureEditorViewModel>? TextureRequested;

    /// <summary>
    /// Asks what to do with the listed unsaved files before an action would lose their edits: save them, discard them, or
    /// cancel the action with null. Without it, the edits are dropped.
    /// </summary>
    public Func<IReadOnlyList<string>, Task<UnsavedAssetsChoice?>>? AskUnsaved { get; set; }

    /// <summary>The viewport's lighting and post-processing, tweaked beside the inspector.</summary>
    [ObservableProperty]
    public partial ViewSettingsViewModel ViewSettings { get; private set; }

    /// <summary>The path of the mood file the viewport draws with, marked with an asterisk when it has unsaved edits.</summary>
    public string MoodHeading
    {
        get
        {
            if (_mood is null)
            {
                return DefaultMoodHeading;
            }

            if (_content.IsUnsaved(_mood.Path))
            {
                return $"{_mood.Path} *";
            }

            return _mood.Path;
        }
    }

    /// <summary>What the Data tab edits: the form of the data asset or the rows of the table last opened in the content browser, until it is closed.</summary>
    [ObservableProperty]
    public partial object? DataContent { get; private set; }

    /// <summary>The open data asset's path, marked with an asterisk when it has unsaved edits.</summary>
    public string DataHeading
    {
        get
        {
            if (_dataPath is null)
            {
                return "Open a data asset or table in the content browser to edit it.";
            }

            if (_content.IsUnsaved(_dataPath))
            {
                return $"{_dataPath} *";
            }

            return _dataPath;
        }
    }

    /// <summary>The Data tab's name: the open data asset's file without its folder and extension, starred while it is unsaved.</summary>
    public string DataTitle
    {
        get
        {
            if (_dataPath is null)
            {
                return "Data";
            }

            string name = Path.GetFileNameWithoutExtension(_dataPath);
            if (_content.IsUnsaved(_dataPath))
            {
                return $"{name} *";
            }

            return name;
        }
    }

    public ViewportViewModel Viewport { get; }

    public InspectorViewModel Inspector { get; }

    /// <summary>The open scene's entities, to select one that is hard to click in the viewport.</summary>
    public SceneTreeViewModel SceneTree { get; }

    /// <summary>The Entity tab: the selected entity's whole tree.</summary>
    public EntityTreeViewModel EntityTree { get; }

    /// <summary>The content folder's files; opening a scene shows it, opening a material shows it in the material editor.</summary>
    public AssetBrowserViewModel Assets { get; }

    /// <summary>The editors opened in place, as tabs over the viewport.</summary>
    public DocumentTabs Documents { get; } = new();

    /// <summary>Runs the project's game on the open scene as it is now, without saving it.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    public void Play()
    {
        try
        {
            LightingMood? mood = FindMoodToPlay();
            if (_player!.Play(mood))
            {
                Viewport.Status = $"Playing {Viewport.Scene.ScenePath} in the game.";
                return;
            }

            Viewport.Status = "Open a scene file to play it.";
        }
        catch (Exception exception) when (exception is IOException or Win32Exception or InvalidOperationException)
        {
            Viewport.Status = $"Can't play: {exception.Message}";
        }
    }

    /// <summary>Builds the project's game again and reloads its code into the scene, keeping the scene's unsaved edits; the creator is left first.</summary>
    [RelayCommand(CanExecute = nameof(CanCompile))]
    public async Task Compile()
    {
        Viewport.LeaveCreator();
        Viewport.Status = "Compiling the project's code...";
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            int modules = await _compiler!.Compile();
            Assets.ListDataTypes(Viewport.Scene.Game.Data);
            double seconds = watch.Elapsed.TotalSeconds;
            Viewport.Status = string.Create(CultureInfo.InvariantCulture, $"Compiled and reloaded {modules} game module(s) in {seconds:0.0} s.");
        }
        catch (ProjectBuildException exception)
        {
            string error = GetFirstError(exception.Message);
            Viewport.Status = $"Compile failed: {error}";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or BadImageFormatException or TypeLoadException or MissingMethodException)
        {
            Viewport.Status = $"Compiled, but can't reload the game's code: {exception.Message}";
        }
    }

    /// <summary>Opens the project's solution in Visual Studio.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenInVisualStudio))]
    public void OpenInVisualStudio()
    {
        try
        {
            VisualStudio.Open(_solution!);
            Viewport.Status = $"Opening {Path.GetFileName(_solution)} in Visual Studio.";
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            Viewport.Status = $"Can't open {_solution}: {exception.Message}";
        }
    }

    public string Title
    {
        get
        {
            string? scene = Viewport.Scene.ScenePath;
            string title = "FlickerVoid Editor";
            if (scene is not null)
            {
                title = $"{scene} - {title}";
            }

            if (HasUnsavedChanges)
            {
                title = $"* {title}";
            }

            return title;
        }
    }

    /// <summary>Whether materials or moods hold edits that are not saved.</summary>
    public bool HasUnsaved => _content.HasUnsaved;

    /// <summary>Whether the scene, materials or moods hold edits that are not saved.</summary>
    public bool HasUnsavedChanges => _content.HasUnsaved || Viewport.Scene.IsUnsaved;

    /// <summary>The files with unsaved edits, the open scene first, for the prompt shown before the editor closes.</summary>
    public IReadOnlyList<string> UnsavedPaths
    {
        get
        {
            List<string> paths = [];
            EditorScene scene = Viewport.Scene;
            if (scene.IsUnsaved && scene.ScenePath is not null)
            {
                paths.Add(scene.ScenePath);
            }

            paths.AddRange(_content.UnsavedPaths);
            return paths;
        }
    }

    /// <summary>Saves the open scene and every material and mood with unsaved edits.</summary>
    public void SaveAll()
    {
        if (Viewport.Scene.IsUnsaved)
        {
            Viewport.Save();
        }

        if (_content.HasUnsaved)
        {
            SaveAssets();
        }
    }

    /// <summary>Saves every material and mood with unsaved edits.</summary>
    [RelayCommand(CanExecute = nameof(HasUnsaved))]
    public void SaveAssets()
    {
        int count = _content.UnsavedPaths.Count;
        _content.SaveAll();
        Viewport.Status = $"Saved {count} asset(s).";
    }

    /// <summary>Forgets every unsaved material and mood edit, going back to the files on disk.</summary>
    [RelayCommand(CanExecute = nameof(HasUnsaved))]
    public void DiscardAssets()
    {
        int count = _content.UnsavedPaths.Count;
        _content.DiscardAll();
        Viewport.Status = $"Discarded the edits of {count} asset(s).";
    }

    private bool CanPlay() => _player is not null;

    /// <summary>
    /// The mood the viewport draws, lens included, when the game would not draw it from the scene's files (the default mood
    /// edited, another mood file, or the scene's mood with unsaved edits), else null, so the game lights the scene with its own
    /// mood, lens, environment and clock.
    /// </summary>
    private LightingMood? FindMoodToPlay()
    {
        if (_mood is null)
        {
            LightingMood defaults = new();
            if (Viewport.Mood == defaults)
            {
                return null;
            }

            return Viewport.GetLayeredMood();
        }

        bool scenesOwn = _mood.Path == _sceneMood && !_content.IsUnsaved(_mood.Path);
        if (scenesOwn)
        {
            return null;
        }

        return Viewport.GetLayeredMood();
    }

    private bool CanCompile() => _compiler is not null;

    private bool CanOpenInVisualStudio() => _solution is not null;

    /// <summary>The build output's first compiler error, else its first line, for the status bar.</summary>
    private static string GetFirstError(string output)
    {
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string line in lines)
        {
            if (line.Contains(": error ", StringComparison.Ordinal))
            {
                return line;
            }
        }

        if (lines.Length == 0)
        {
            return output;
        }

        return lines[0];
    }

    private void HandleUnsavedChanged()
    {
        OnPropertyChanged(nameof(HasUnsaved));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(Title));
        RaiseTopBarChanged();
        OnPropertyChanged(nameof(MoodHeading));
        OnPropertyChanged(nameof(DataHeading));
        OnPropertyChanged(nameof(DataTitle));
        OnPropertyChanged(nameof(CreatorHeading));
        OnPropertyChanged(nameof(IsCreatorUnsaved));
        SaveAssetsCommand.NotifyCanExecuteChanged();
        DiscardAssetsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Keeps the unsaved edits of a file changed on disk and says so: saving overwrites the change, discarding takes it.</summary>
    private void HandleChangedOnDisk(string fullPath)
    {
        string path = Path.GetRelativePath(_content.RootDirectory, fullPath);
        Viewport.Status = $"{path} changed on disk; its unsaved edits are kept. Save to overwrite it or discard to take the change.";
    }

    /// <summary>Opens a scene in place of the open one, first asking what to do with the open scene's unsaved edits.</summary>
    private async Task OpenScene(string path)
    {
        EditorScene scene = Viewport.Scene;
        if (scene.IsUnsaved && scene.ScenePath is not null && AskUnsaved is not null)
        {
            string[] unsaved = [scene.ScenePath];
            UnsavedAssetsChoice? choice = await AskUnsaved(unsaved);
            switch (choice)
            {
                case UnsavedAssetsChoice.Save:
                    Viewport.Save();
                    break;
                case UnsavedAssetsChoice.Discard:
                    break;
                default:
                    return;
            }
        }

        Viewport.LeaveCreator();
        try
        {
            Viewport.Scene.Open(path);
            Viewport.Status = $"Opened {path}.";
        }
        catch (Exception exception) when (exception is InvalidDataException or FileNotFoundException or DirectoryNotFoundException
            or System.Text.Json.JsonException or KeyNotFoundException or ArgumentException or InvalidOperationException)
        {
            Viewport.Status = $"Can't open {path}: {exception.Message}";
        }
    }

    private void HandleSceneOpened()
    {
        OnPropertyChanged(nameof(Title));
        RaiseTopBarChanged();
        ShowSceneMood();
    }

    /// <summary>Draws with the mood the open scene names, or the default mood when it names none.</summary>
    private void ShowSceneMood()
    {
        _sceneMood = SceneMood.Find(Viewport.Scene.Game.World);
        if (_sceneMood is not null)
        {
            OpenMood(_sceneMood);
            return;
        }

        _mood = null;
        Viewport.MoodPath = null;
        Viewport.Mood = new LightingMood();
        ContentFileFinder files = new(_content);
        ViewSettings = new ViewSettingsViewModel(Viewport.Mood, files.Find);
        OnPropertyChanged(nameof(MoodHeading));
        WatchHistory();
    }

    /// <summary>Draws with the mood the scene is given in the inspector, once the name changes.</summary>
    private void HandleSceneEdited()
    {
        string? named = SceneMood.Find(Viewport.Scene.Game.World);
        if (named == _sceneMood)
        {
            return;
        }

        _sceneMood = named;
        if (named is not null)
        {
            OpenMood(named);
        }
    }

    /// <summary>The kinds of entity the viewport's Add menu (Shift+A) offers, each in its group.</summary>
    public IReadOnlyList<MenuChoice> AddChoices => field ??= [.. EntityPreset.All.Select(ToChoice)];

    /// <summary>Adds an entity of the preset at the camera's pivot to the open scene and selects it.</summary>
    public void AddEntity(EntityPreset preset)
    {
        EditorScene scene = Viewport.Scene;
        if (scene.ScenePath is null)
        {
            Viewport.Status = "Open a scene before adding entities: double-click one in the content browser.";
            return;
        }

        EntityData data = preset.Create(scene.Game.World, scene.Entities, Viewport.Camera.Pivot);
        Entity entity = scene.AddEntity(data);
        scene.Selection.Select(entity);
        Viewport.Status = $"Added {data.Name}.";
    }

    private MenuChoice ToChoice(EntityPreset preset)
    {
        return new MenuChoice(preset.Name, () => AddEntity(preset), preset.Group, preset.Icon);
    }
}
