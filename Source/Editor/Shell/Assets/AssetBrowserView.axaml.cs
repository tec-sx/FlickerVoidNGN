using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Editor.Assets;

/// <summary>The asset browser's folder tree and file list; a double-click or Enter opens the selected file.</summary>
public sealed partial class AssetBrowserView : UserControl
{
    public AssetBrowserView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void HandleDoubleTapped(object? sender, TappedEventArgs e)
    {
        OpenSelected(sender);
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OpenSelected(sender);
        }
    }

    private void OpenSelected(object? sender)
    {
        if (sender is ListBox { SelectedItem: AssetEntry asset } && DataContext is AssetBrowserViewModel browser)
        {
            browser.OpenCommand.Execute(asset);
        }
    }
}
