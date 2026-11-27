using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Assets.Data;
using FlickerVoid.Editor.Assets.Environments;
using FlickerVoid.Editor.Assets.Flows;
using FlickerVoid.Editor.Assets.Moods;
using FlickerVoid.Editor.Assets.Textures;
using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Editor.Core.Environments;
using FlickerVoid.Editor.Core.Flows;
using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Editor.Materials;
using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Audio.Sounds;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;

namespace FlickerVoid.Editor;

public sealed partial class MainWindowViewModel
{
    /// <summary>The flow graph's undo and redo steps, for its editor window.</summary>
    public UndoHistory GetFlowHistory(string path) => _content.GetHistory(path);

    /// <summary>Forgets a flow graph whose editor window closed, so undo there no longer opens it.</summary>
    public void CloseFlow(string path) => _openFlows.Remove(path);

    /// <summary>The music cue's undo and redo steps, for its editor window.</summary>
    public UndoHistory GetMusicHistory(string path) => _content.GetHistory(path);

    /// <summary>Forgets a music cue whose editor window closed, so undo there no longer opens it.</summary>
    public void CloseMusic(string path) => _openMusic.Remove(path);

    /// <summary>The environment file's undo and redo steps, for its editor window.</summary>
    public UndoHistory GetEnvironmentHistory(string path) => _content.GetHistory(path);

    /// <summary>Forgets an environment whose editor window closed, and draws the scene's own environment again.</summary>
    public void CloseEnvironment(string path)
    {
        _openEnvironments.Remove(path);
        if (Viewport.EnvironmentPath is { } shown && ContentManager.PathComparer.Equals(shown, path))
        {
            Viewport.EnvironmentPath = null;
        }
    }

    /// <summary>A material editor over the scene's content, previewing on the GPU when there is one.</summary>
    public MaterialEditorViewModel CreateMaterialEditor()
    {
        EditorScene scene = Viewport.Scene;
        MaterialPreview? preview = null;
        string status = "";
        try
        {
            preview = new MaterialPreview(scene.Game.LoggerFactory, _content.RootDirectory);
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

    private void OpenAsset(AssetEntry asset)
    {
        _openers ??= CreateOpeners();
        if (_openers.TryGetValue(asset.Type, out Action<AssetEntry>? open))
        {
            open(asset);
        }
    }

    /// <summary>The editor each type of file opens in; files of other types open nothing.</summary>
    private Dictionary<AssetType, Action<AssetEntry>> CreateOpeners()
    {
        return new Dictionary<AssetType, Action<AssetEntry>>
        {
            [AssetTypes.Scene] = OpenSceneAsset,
            [AssetTypes.Material] = asset => MaterialRequested?.Invoke(asset.Path),
            [AssetTypes.Mood] = asset => ShowMood(asset.Path),
            [AssetTypes.Texture] = asset => OpenTexture(asset.Path),
            [AssetTypes.Canvas] = asset => OpenTexture(asset.Path),
            [AssetTypes.Data] = asset => OpenData(asset.Path),
            [AssetTypes.Music] = asset => OpenData(asset.Path),
            [AssetTypes.Sound] = asset => OpenData(asset.Path),
            [AssetTypes.Table] = asset => OpenTable(asset.Path),
            [AssetTypes.Flow] = asset => OpenFlow(asset.Path),
            [AssetTypes.Environment] = asset => OpenEnvironment(asset.Path),
            [AssetTypes.Code] = OpenCode,
            [AssetTypes.Vehicle] = asset => OpenCreation(asset.Path),
            [AssetTypes.Building] = asset => OpenCreation(asset.Path),
            [AssetTypes.Prop] = asset => OpenCreation(asset.Path),
            [AssetTypes.Foliage] = asset => OpenCreation(asset.Path),
        };
    }

    private async void OpenSceneAsset(AssetEntry asset)
    {
        await OpenScene(asset.Path);
    }

    /// <summary>Edits an image's settings or a canvas in a texture editor window, previewed from the source images.</summary>
    private void OpenTexture(string path)
    {
        if (path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
        {
            Viewport.Status = $"{path} is built; open its source image instead.";
            return;
        }

        ContentFileFinder files = new(_content);
        TextureEditorViewModel editor;
        try
        {
            editor = new TextureEditorViewModel(_content, path, Assets.GetSourcePath, files.Find);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            Viewport.Status = $"Can't open {path}: {exception.Message}";
            return;
        }

        TextureRequested?.Invoke(editor);
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
        if (document.Asset is MusicCue cue)
        {
            JsonTypeInfo trackType = types.Options.GetTypeInfo(typeof(MusicTrack));
            ClipCatalog catalog = new(ReadClip);
            MusicEditorViewModel editor = new(title, cue, typeInfo, trackType, document.Hold, catalog, files.Find);
            _openMusic.Add(path);
            MusicRequested?.Invoke(path, editor);
            return;
        }

        ShowData(path, new DataObjectViewModel(title, document.Asset, typeInfo, document.Hold, files.Find));
    }

    private SoundClip? ReadClip(string file)
    {
        string full = _content.GetFullPath(file);
        if (!File.Exists(full))
        {
            return null;
        }

        try
        {
            if (full.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                using FileStream stream = File.OpenRead(full);
                return OggReader.Read(stream);
            }

            byte[] bytes = File.ReadAllBytes(full);
            return WavReader.Read(bytes);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
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
        _openFlows.Add(path);
        FlowRequested?.Invoke(path, graph);
    }

    /// <summary>
    /// Edits the environment in its editor window, its edits held unsaved like a flow graph's, and draws the viewport with it at
    /// the editor's hour; reopened after an undo, it keeps the hour it was at.
    /// </summary>
    private void OpenEnvironment(string path)
    {
        EnvironmentDocument document;
        try
        {
            document = new EnvironmentDocument(_content, path);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or IOException or UnauthorizedAccessException)
        {
            Viewport.Status = $"Can't open {path}: {exception.Message}";
            return;
        }

        ContentFileFinder files = new(_content);
        string title = Path.GetFileNameWithoutExtension(path);
        EnvironmentEditorViewModel editor = new(title, document.Environment, document.Hold, files.Find);
        if (_openEnvironments.TryGetValue(path, out EnvironmentEditorViewModel? open))
        {
            editor.Hour = open.Hour;
            editor.Speed = open.Speed;
            open.PropertyChanged -= HandleEnvironmentChanged;
        }
        else
        {
            editor.Hour = Viewport.Hour;
        }

        _openEnvironments[path] = editor;
        editor.PropertyChanged += HandleEnvironmentChanged;
        Viewport.EnvironmentPath = path;
        Viewport.Hour = editor.Hour;
        DayDockPath = path;
        DayDock = editor;
        EnvironmentRequested?.Invoke(path, editor);
    }

    /// <summary>The environment the Day dock under the viewport edits, or null while the dock is closed.</summary>
    [ObservableProperty]
    public partial EnvironmentEditorViewModel? DayDock { get; private set; }

    /// <summary>The file of the environment in the Day dock.</summary>
    public string? DayDockPath { get; private set; }

    /// <summary>Whether the Day dock is open.</summary>
    public bool IsDayDockOpen => DayDock is not null;

    partial void OnDayDockChanged(EnvironmentEditorViewModel? value) => OnPropertyChanged(nameof(IsDayDockOpen));

    /// <summary>Closes the Day dock; its environment's edits stay held unsaved.</summary>
    [RelayCommand]
    public void CloseDayDock()
    {
        if (DayDockPath is { } path)
        {
            CloseEnvironment(path);
        }

        DayDockPath = null;
        DayDock = null;
    }

    private void HandleEnvironmentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EnvironmentEditorViewModel.Hour) && sender is EnvironmentEditorViewModel editor)
        {
            Viewport.Hour = editor.Hour;
        }
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
        OnPropertyChanged(nameof(DataTitle));
        ShowContext(DataTab);
        WatchHistory();
    }

    /// <summary>Opens the mood file from the content browser and brings the outliner's View tab, which edits it, in front.</summary>
    private void ShowMood(string path)
    {
        OpenMood(path);
        OutlinerTab = ViewTab;
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

        ContentFileFinder files = new(_content);
        ViewSettingsViewModel settings = new(_mood.Mood, files.Find);
        settings.Edited += _mood.Hold;
        Viewport.Mood = _mood.Mood;
        Viewport.MoodPath = path;
        ViewSettings = settings;
        OnPropertyChanged(nameof(MoodHeading));
        Viewport.Status = $"Lighting the viewport with {path}.";
        WatchHistory();
    }

    /// <summary>Shows the open mood or creation again when its file changed other than by an edit here: discarded or changed on disk.</summary>
    private void HandleContentChanged(string fullPath)
    {
        ShowCreationLook(fullPath);
        if (Viewport.CreatorFile is { } creation && !_content.IsUnsaved(creation))
        {
            ShowCreation(fullPath);
        }

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

    /// <summary>Shows the mood, data asset, table, flow graph or creation again after an undo or redo changed its file.</summary>
    private void HandleRestored(string fullPath)
    {
        ShowCreation(fullPath);
        if (_mood is not null && IsPath(_mood.Path, fullPath))
        {
            OpenMood(_mood.Path);
        }

        if (_dataPath is not null && IsPath(_dataPath, fullPath))
        {
            if (_dataPath.EndsWith(".fvtable", StringComparison.OrdinalIgnoreCase))
            {
                OpenTable(_dataPath);
            }
            else
            {
                OpenData(_dataPath);
            }
        }

        foreach (string environment in _openEnvironments.Keys.ToList())
        {
            if (IsPath(environment, fullPath))
            {
                OpenEnvironment(environment);
            }
        }

        foreach (string music in _openMusic.ToList())
        {
            if (IsPath(music, fullPath))
            {
                OpenData(music);
            }
        }

        foreach (string flow in _openFlows.ToList())
        {
            if (IsPath(flow, fullPath))
            {
                OpenFlow(flow);
            }
        }
    }

    private bool IsPath(string path, string fullPath)
    {
        string full = _content.GetFullPath(path);
        return ContentManager.PathComparer.Equals(full, fullPath);
    }
}
