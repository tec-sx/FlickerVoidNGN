using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Editor.Viewports;

public sealed partial class ToolRailView : UserControl
{
    public ToolRailView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
