using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Projects;

/// <summary>Lists the projects to open, browses for another project file and creates new ones.</summary>
public sealed partial class ProjectPickerWindow : Window
{
    private static readonly FilePickerFileType ProjectFiles = new("FlickerVoid projects") { Patterns = ["*" + EditorProject.Extension] };

    public ProjectPickerWindow()
    {
        AvaloniaXamlLoader.Load(this);
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
