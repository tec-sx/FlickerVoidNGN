using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Editor.Inspector;
using FlickerVoid.Engine.Editor.Viewports;

namespace FlickerVoid.Engine.Editor.Shell;

/// <summary>The editor's main window: its title, the scene view and the inspector.</summary>
public sealed class MainWindowViewModel(ViewportViewModel viewport, InspectorViewModel inspector) : ObservableObject
{
    public ViewportViewModel Viewport { get; } = viewport;

    public InspectorViewModel Inspector { get; } = inspector;

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
