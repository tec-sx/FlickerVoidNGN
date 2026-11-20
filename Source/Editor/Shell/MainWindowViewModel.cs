using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Editor.Assets;
using FlickerVoid.Engine.Editor.Inspector;
using FlickerVoid.Engine.Editor.Materials;
using FlickerVoid.Engine.Editor.Scenes;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Editor.Viewports;

namespace FlickerVoid.Engine.Editor.Shell;

/// <summary>The editor's main window: its title, the scene view, the asset browser, the inspector and the view settings.</summary>
public sealed class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(ViewportViewModel viewport, InspectorViewModel inspector)
    {
        Viewport = viewport;
        Inspector = inspector;
        ViewSettings = new ViewSettingsViewModel(viewport.Mood);
        AssetCatalog catalog = new(viewport.Scene.Content.RootDirectory);
        Assets = new AssetBrowserViewModel(catalog);
        Assets.Opened += OpenAsset;
        viewport.Scene.Opened += HandleSceneOpened;
    }

    /// <summary>Raised to show a material in the material editor, by its path in the content folder.</summary>
    public event Action<string>? MaterialRequested;

    /// <summary>The viewport's lighting and post-processing, tweaked beside the inspector.</summary>
    public ViewSettingsViewModel ViewSettings { get; }

    public ViewportViewModel Viewport { get; }

    public InspectorViewModel Inspector { get; }

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

        MaterialEditorViewModel editor = new(scene, preview);
        if (status.Length > 0)
        {
            editor.Status = status;
        }

        return editor;
    }

    public string Title
    {
        get
        {
            string? scene = Viewport.Scene.ScenePath;
            if (scene is null)
            {
                return "FlickerVoid Editor";
            }

            return $"{scene} - FlickerVoid Editor";
        }
    }

    private void OpenAsset(AssetEntry asset)
    {
        switch (asset.Kind)
        {
            case AssetKind.Scene:
                OpenScene(asset.Path);
                break;
            case AssetKind.Material:
                MaterialRequested?.Invoke(asset.Path);
                break;
        }
    }

    private void OpenScene(string path)
    {
        try
        {
            Viewport.Scene.Open(path);
            Viewport.Status = $"Opened {path}.";
        }
        catch (InvalidDataException exception)
        {
            Viewport.Status = $"Can't open {path}: {exception.Message}";
        }
    }

    private void HandleSceneOpened()
    {
        OnPropertyChanged(nameof(Title));
    }
}
