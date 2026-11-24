using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Assets.Data;
using FlickerVoid.Editor.Assets.Flows;
using FlickerVoid.Editor.Assets.Moods;
using FlickerVoid.Editor.Assets.Textures;
using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Editor.Core.Flows;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Editor.Materials;
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
            case AssetKind.Texture:
            case AssetKind.Canvas:
                OpenTexture(asset.Path);
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
        _openFlows.Add(path);
        FlowRequested?.Invoke(path, graph);
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
        WatchHistory();
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
        ViewSettings = settings;
        OnPropertyChanged(nameof(MoodHeading));
        Viewport.Status = $"Lighting the viewport with {path}.";
        WatchHistory();
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

    /// <summary>Shows the mood, data asset, table or flow graph again after an undo or redo changed its file.</summary>
    private void HandleRestored(string fullPath)
    {
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
