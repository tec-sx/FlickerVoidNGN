using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Editor.Assets;

/// <summary>
/// The asset browser's folder tree and file list; a double-click or Enter opens the selected file, and a file dragged a few
/// pixels starts a drag the viewport can take.
/// </summary>
public sealed partial class AssetBrowserView : UserControl
{
    private const double DragThreshold = 4;

    private PointerPressedEventArgs? _pressed;
    private AssetEntry? _pressedAsset;

    public AssetBrowserView()
    {
        AvaloniaXamlLoader.Load(this);
        ListBox list = this.GetControl<ListBox>("AssetList");
        list.AddHandler(PointerPressedEvent, HandlePointerPressed, RoutingStrategies.Tunnel);
        list.AddHandler(PointerMovedEvent, HandlePointerMoved, RoutingStrategies.Tunnel);
        list.AddHandler(PointerReleasedEvent, HandlePointerReleased, RoutingStrategies.Tunnel);
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(this);
        bool left = point.Properties.IsLeftButtonPressed;
        StyledElement? source = e.Source as StyledElement;
        if (left && source?.DataContext is AssetEntry asset)
        {
            _pressed = e;
            _pressedAsset = asset;
        }
    }

    private async void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed is null || _pressedAsset is null)
        {
            return;
        }

        Point start = _pressed.GetPosition(this);
        Point now = e.GetPosition(this);
        Point moved = now - start;
        if (Math.Abs(moved.X) < DragThreshold && Math.Abs(moved.Y) < DragThreshold)
        {
            return;
        }

        PointerPressedEventArgs pressed = _pressed;
        AssetEntry asset = _pressedAsset;
        _pressed = null;
        _pressedAsset = null;
        await AssetDrag.StartAsync(pressed, asset);
    }

    private void HandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressed = null;
        _pressedAsset = null;
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
