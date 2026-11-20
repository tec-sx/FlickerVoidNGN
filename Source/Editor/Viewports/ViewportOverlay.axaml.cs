using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Engine.Editor.Viewports;

/// <summary>
/// The controls drawn over the viewport: Unreal-style tool and view icons in the top left, and Blender's navigation gizmo with its
/// pan and zoom handles in the top right.
/// </summary>
public sealed partial class ViewportOverlay : UserControl
{
    public ViewportOverlay()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
