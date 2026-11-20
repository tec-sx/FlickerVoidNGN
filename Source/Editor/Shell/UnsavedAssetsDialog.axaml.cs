using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Editor;

/// <summary>Asks whether to save or discard the unsaved assets before the editor closes; closes with null to cancel.</summary>
public sealed partial class UnsavedAssetsDialog : Window
{
    public UnsavedAssetsDialog()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public UnsavedAssetsDialog(IReadOnlyList<string> paths)
        : this()
    {
        ItemsControl list = this.FindControl<ItemsControl>("Paths")!;
        list.ItemsSource = paths;
    }

    private void HandleSaveClick(object? sender, RoutedEventArgs e) => Close(UnsavedAssetsChoice.Save);

    private void HandleDiscardClick(object? sender, RoutedEventArgs e) => Close(UnsavedAssetsChoice.Discard);

    private void HandleCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
