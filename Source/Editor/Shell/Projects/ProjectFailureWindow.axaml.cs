using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Editor.Projects;

/// <summary>Shows why a project failed to open: the exception's log, with the build's output when the build failed.</summary>
public sealed partial class ProjectFailureWindow : Window
{
    public ProjectFailureWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void HandleCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectOpenFailure failure && Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(failure.Log);
        }
    }

    private void HandleCloseClick(object? sender, RoutedEventArgs e) => Close();
}
