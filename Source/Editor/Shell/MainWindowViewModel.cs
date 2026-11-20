using System.ComponentModel;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Engine.Editor.Assets;
using FlickerVoid.Engine.Editor.Inspector;
using FlickerVoid.Engine.Editor.Materials;
using FlickerVoid.Engine.Editor.Projects;
using FlickerVoid.Engine.Editor.Scenes;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Editor.Viewports;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

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
        SceneTree = new SceneTreeViewModel(viewport.Scene);
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
        catch (Exception exception) when (exception is InvalidDataException or FileNotFoundException or DirectoryNotFoundException
            or System.Text.Json.JsonException or KeyNotFoundException or ArgumentException or InvalidOperationException)
        {
            Viewport.Status = $"Can't open {path}: {exception.Message}";
        }
    }

    private void HandleSceneOpened()
    {
        OnPropertyChanged(nameof(Title));
    }

    /// <summary>The kinds of entity the Add menu offers.</summary>
    public IReadOnlyList<EntityPreset> Presets => EntityPreset.All;

    /// <summary>Adds an entity of the preset at the camera's pivot to the open scene and selects it.</summary>
    [RelayCommand]
    private void AddEntity(EntityPreset preset)
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
}
