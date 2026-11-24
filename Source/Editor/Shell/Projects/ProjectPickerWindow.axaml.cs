using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Projects;

/// <summary>
/// Lists the projects to open, browses for another project file and creates new ones; while one opens, its splash covers the
/// picker until the editor replaces both, and a failure shows the exception's log over the picker.
/// </summary>
public sealed partial class ProjectPickerWindow : Window
{
    private static readonly FilePickerFileType ProjectFiles = new("FlickerVoid projects") { Patterns = ["*" + EditorProject.Extension] };

    private SplashWindow? _splash;

    public ProjectPickerWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ProjectPickerViewModel picker)
        {
            picker.Opening += ShowSplash;
            picker.Failed += ShowFailure;
        }
    }

    /// <summary>Shows the splash as the picker's own window, so it closes with the picker when the editor opens.</summary>
    private void ShowSplash(SplashViewModel splash)
    {
        _splash = new SplashWindow { DataContext = splash };
        _splash.Show(this);
    }

    private async void ShowFailure(ProjectOpenFailure failure)
    {
        _splash?.Close();
        _splash = null;
        ProjectFailureWindow window = new() { DataContext = failure };
        await window.ShowDialog(this);
    }

    private void HandleDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ProjectPickerViewModel picker && picker.SelectedProject is { } project)
        {
            picker.OpenCommand.Execute(project);
        }
    }

    private async void HandleBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectPickerViewModel picker)
        {
            return;
        }

        FilePickerOpenOptions options = new() { Title = "Open a project", FileTypeFilter = [ProjectFiles] };
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
        {
            return;
        }

        await picker.OpenFile(path);
    }
}
