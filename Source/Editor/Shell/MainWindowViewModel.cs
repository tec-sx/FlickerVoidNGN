using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Editor.Viewports;

namespace FlickerVoid.Engine.Editor.Shell;

/// <summary>The editor's main window: its title and the scene view.</summary>
public sealed class MainWindowViewModel(ViewportViewModel viewport) : ObservableObject
{
    public ViewportViewModel Viewport { get; } = viewport;

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
