using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Editor.Viewports;

/// <summary>The Tool tab: the options of the mesh tool in hand, or the numbers and settings of the tools that act at once.</summary>
public sealed partial class ToolOptionsView : UserControl
{
    public ToolOptionsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
