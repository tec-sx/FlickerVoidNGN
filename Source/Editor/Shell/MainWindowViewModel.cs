using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization.Metadata;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Assets.Data;
using FlickerVoid.Editor.Assets.Flows;
using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Editor.Assets.Moods;
using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Editor.Core.Flows;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Editor.Inspector;
using FlickerVoid.Editor.Materials;
using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;
using FlickerVoid.Editor.Scenes;
using FlickerVoid.Editor.Viewports;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;

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
    private readonly UnsavedContent _content;
    private MoodDocument? _mood;
    private string? _dataPath;
    private string? _sceneMood;

    /// <summary>
    /// Shows the scene over <paramref name="content"/>, the scene's content with its unsaved edits; <paramref name="player"/>
    /// plays it in the project's game, when a project is open, and the asset browser shows <paramref name="codeFolder"/>, the
    /// project's source folder.
    /// </summary>
    public MainWindowViewModel(
        ViewportViewModel viewport,
        InspectorViewModel inspector,
        UnsavedContent content,
        ScenePlayer? player = null,
        string? codeFolder = null)
    {
        _player = player;
        _content = content;
        _content.UnsavedChanged += HandleUnsavedChanged;
        _content.ChangedOnDisk += HandleChangedOnDisk;
        _content.Changed += HandleContentChanged;
        Viewport = viewport;
        Inspector = inspector;
        SceneTree = new SceneTreeViewModel(viewport.Scene);
        ViewSettings = new ViewSettingsViewModel(viewport.Mood);
        Assets = new AssetBrowserViewModel(viewport.Scene.Content, viewport.Scene.Game.Data, codeFolder);
        Assets.Opened += OpenAsset;
        viewport.Scene.Opened += HandleSceneOpened;
        viewport.Scene.UnsavedChanged += HandleUnsavedChanged;
        viewport.Scene.Edited += HandleSceneEdited;
        ShowSceneMood();
    }

    /// <summary>Raised to show a flow graph opened in the content browser in a graph editor window.</summary>
    public event Action<FlowGraphViewModel>? FlowRequested;

    /// <summary>Raised to show a material in the material editor, by its path in the content folder.</summary>
    public event Action<string>? MaterialRequested;

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

    /// <summary>What the Data tab edits: the form of the data asset or the rows of the table last opened in the content browser.</summary>
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

    /// <summary>The index of the Data tab under the scene outliner.</summary>
    public const int DataTab = 2;

    /// <summary>The open tab under the scene outliner: 0 inspector, 1 view, 2 data.</summary>
    [ObservableProperty]
    public partial int SideTab { get; set; }

    public ViewportViewModel Viewport { get; }

    public InspectorViewModel Inspector { get; }

    /// <summary>The open scene's entities, to select one that is hard to click in the viewport.</summary>
    public SceneTreeViewModel SceneTree { get; }

    /// <summary>The content folder's files; opening a scene shows it, opening a material shows it in the material editor.</summary>
    public AssetBrowserViewModel Assets { get; }

    /// <summary>A material editor over the scene's content, previewing on the GPU when there is one.</summary>
    public MaterialEditorViewModel CreateMaterialEditor()
    {
        EditorScene scene = Viewport.Scene;
        MaterialPreview? preview = null;
        string status = "";
        try
        {
            preview = new MaterialPreview(scene.Game.LoggerFactory);
        }
        catch (GraphicsUnavailableException exception)
        {
            status = exception.Message;
        }

        MaterialEditorViewModel editor = new(_content, preview);
        if (status.Length > 0)
        {
            editor.Status = status;
        }

        return editor;
    }

    /// <summary>Runs the project's game on the open scene as it is now, without saving it.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    public void Play()
    {
        try
        {
            if (_player!.Play(Viewport.Mood))
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

    private void HandleUnsavedChanged()
    {
        OnPropertyChanged(nameof(HasUnsaved));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(MoodHeading));
        OnPropertyChanged(nameof(DataHeading));
        SaveAssetsCommand.NotifyCanExecuteChanged();
        DiscardAssetsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Keeps the unsaved edits of a file changed on disk and says so: saving overwrites the change, discarding takes it.</summary>
    private void HandleChangedOnDisk(string fullPath)
    {
        string path = Path.GetRelativePath(_content.RootDirectory, fullPath);
        Viewport.Status = $"{path} changed on disk; its unsaved edits are kept. Save to overwrite it or discard to take the change.";
    }

    /// <summary>Shows the content file in the asset browser and opens it as a double-click would; a missing file is reported.</summary>
    public void OpenContentFile(string path)
    {
        AssetEntry? asset = Assets.Reveal(path);
        if (asset is null)
        {
            Viewport.Status = $"{path} is not in the content folder.";
            return;
        }

        OpenAsset(asset);
    }

    private async void OpenAsset(AssetEntry asset)
    {
        switch (asset.Kind)
        {
            case AssetKind.Scene:
                await OpenScene(asset.Path);
                break;
            case AssetKind.Material:
                MaterialRequested?.Invoke(asset.Path);
                break;
            case AssetKind.Mood:
                OpenMood(asset.Path);
                break;
            case AssetKind.Data:
                OpenData(asset.Path);
                break;
            case AssetKind.Table:
                OpenTable(asset.Path);
                break;
            case AssetKind.Flow:
                OpenFlow(asset.Path);
                break;
            case AssetKind.Code:
                OpenCode(asset);
                break;
        }
    }

    /// <summary>Opens a code file in the program the system opens it with, the IDE.</summary>
    private void OpenCode(AssetEntry asset)
    {
        string fullPath = Assets.GetFullPath(asset);
        ProcessStartInfo start = new(fullPath) { UseShellExecute = true };
        try
        {
            using Process? process = Process.Start(start);
        }
        catch (Win32Exception exception)
        {
            Viewport.Status = $"Can't open {fullPath}: {exception.Message}";
        }
    }

    /// <summary>Edits the data asset in the Data tab, its edits held unsaved like a material's; a file that can't be read is reported.</summary>
    private void OpenData(string path)
    {
        EditorScene scene = Viewport.Scene;
        DataTypes types = scene.Game.Data;
        DataDocument document;
        try
        {
            document = new DataDocument(_content, types, path);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Viewport.Status = $"Can't open {path}: {exception.Message}";
            return;
        }

        JsonTypeInfo typeInfo = types.Options.GetTypeInfo(document.Asset.GetType());
        ContentFileFinder files = new(_content);
        string title = Path.GetFileNameWithoutExtension(path);
        ShowData(path, new DataObjectViewModel(title, document.Asset, typeInfo, document.Hold, files.Find));
    }

    /// <summary>Edits the flow graph in a graph editor window, its edits held unsaved like a data asset's.</summary>
    private void OpenFlow(string path)
    {
        EditorScene scene = Viewport.Scene;
        DataTypes types = scene.Game.Data;
        FlowDocument document;
        try
        {
            document = new FlowDocument(_content, types, path);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Viewport.Status = $"Can't open {path}: {exception.Message}";
            return;
        }

        ContentFileFinder files = new(_content);
        string title = Path.GetFileNameWithoutExtension(path);
        FlowGraphViewModel graph = new(title, document.Graph, types, document.Hold, files.Find);
        FlowRequested?.Invoke(graph);
    }

    /// <summary>Edits the table's rows in the Data tab, held unsaved like a data asset's.</summary>
    private void OpenTable(string path)
    {
        EditorScene scene = Viewport.Scene;
        TableDocument document;
        try
        {
            document = new TableDocument(_content, scene.Game.Data, path);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Viewport.Status = $"Can't open {path}: {exception.Message}";
            return;
        }

        ContentFileFinder files = new(_content);
        ShowData(path, new TableViewModel(document, files.Find));
    }

    private void ShowData(string path, object content)
    {
        _dataPath = path;
        DataContent = content;
        OnPropertyChanged(nameof(DataHeading));
        SideTab = DataTab;
    }

    /// <summary>Draws the viewport with the mood file and edits it in the View tab; a file that can't be read leaves the view as it is.</summary>
    private void OpenMood(string path)
    {
        try
        {
            _mood = new MoodDocument(_content, path);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Viewport.Status = $"Can't open {path}: {exception.Message}";
            return;
        }

        ViewSettingsViewModel settings = new(_mood.Mood);
        settings.Edited += _mood.Hold;
        Viewport.Mood = _mood.Mood;
        ViewSettings = settings;
        OnPropertyChanged(nameof(MoodHeading));
        Viewport.Status = $"Lighting the viewport with {path}.";
    }

    /// <summary>Shows the open mood again when its file changed other than by an edit here: discarded or changed on disk.</summary>
    private void HandleContentChanged(string fullPath)
    {
        if (_mood is null || _content.IsUnsaved(_mood.Path))
        {
            return;
        }

        string open = _content.GetFullPath(_mood.Path);
        if (ContentManager.PathComparer.Equals(open, fullPath))
        {
            OpenMood(_mood.Path);
        }
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
        Viewport.Mood = new LightingMood();
        ViewSettings = new ViewSettingsViewModel(Viewport.Mood);
        OnPropertyChanged(nameof(MoodHeading));
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
