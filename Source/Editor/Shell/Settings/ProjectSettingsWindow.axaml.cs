using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Editor.Settings;

/// <summary>Shows a <see cref="Core.Settings.ProjectSettingsViewModel"/>: the project's settings, by page.</summary>
public sealed partial class ProjectSettingsWindow : Window
{
    public ProjectSettingsWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
