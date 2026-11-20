using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Engine.Editor.Shell;

/// <summary>The editor's main window: a toolbar, the scene view and a status bar.</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
