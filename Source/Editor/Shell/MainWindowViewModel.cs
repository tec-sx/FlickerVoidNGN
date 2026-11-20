using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Engine.Editor.Assets;
using FlickerVoid.Engine.Editor.Inspector;
using FlickerVoid.Engine.Editor.Materials;
using FlickerVoid.Engine.Editor.Projects;
using FlickerVoid.Engine.Editor.Scenes;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Editor.Viewports;

namespace FlickerVoid.Engine.Editor.Shell;

/// <summary>The editor's main window: its title, the scene view, the asset browser, the inspector, the view settings and Play.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ScenePlayer? _player;

    /// <summary>Shows the scene; <paramref name="player"/> plays it in the project's game, when a project is open.</summary>
    public MainWindowViewModel(ViewportViewModel viewport, InspectorViewModel inspector, ScenePlayer? player = null)
    {
        _player = player;
        Viewport = viewport;
        Inspector = inspector;
        ViewSettings = new ViewSettingsViewModel(viewport.Mood);
        Assets = new AssetBrowserViewModel(viewport.Scene.Content);
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

    /// <summary>Runs the project's game on the open scene as it is now, without saving it.</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    public void Play()
    {
        try
        {
            if (_player!.Play())
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
            if (scene is null)
            {
                return "FlickerVoid Editor";
            }

            return $"{scene} - FlickerVoid Editor";
        }
    }

    private bool CanPlay() => _player is not null;

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
