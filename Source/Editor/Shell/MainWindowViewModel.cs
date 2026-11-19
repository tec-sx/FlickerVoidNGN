using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Editor.Inspector;
using FlickerVoid.Engine.Editor.Materials;
using FlickerVoid.Engine.Editor.Scenes;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Editor.Viewports;

namespace FlickerVoid.Engine.Editor.Shell;

/// <summary>The editor's main window: its title, the scene view and the inspector.</summary>
public sealed class MainWindowViewModel(ViewportViewModel viewport, InspectorViewModel inspector) : ObservableObject
{
    public ViewportViewModel Viewport { get; } = viewport;

    public InspectorViewModel Inspector { get; } = inspector;

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
}
