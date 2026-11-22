using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using FlickerVoid.Editor.Core.Inspector;

namespace FlickerVoid.Editor.Inspector;

/// <summary>An asset field's picker; its open button raises <see cref="OpenRequestedEvent"/>, which the window holding it answers.</summary>
public sealed partial class AssetPickerView : UserControl
{
    public static readonly RoutedEvent<OpenAssetEventArgs> OpenRequestedEvent =
        RoutedEvent.Register<AssetPickerView, OpenAssetEventArgs>("OpenRequested", RoutingStrategies.Bubble);

    public AssetPickerView()
    {
        InitializeComponent();
    }

    private void HandlePopupOpened(object? sender, EventArgs e)
    {
        Search.Focus();
    }

    private void HandleRowChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (Rows.SelectedItem is not string path || DataContext is not AssetPickerViewModel picker)
        {
            return;
        }

        Rows.SelectedItem = null;
        picker.Choose(path);
    }

    /// <summary>Enter takes the first match, as in the searchable menus.</summary>
    private void HandleSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not AssetPickerViewModel { Matches.Count: > 0 } picker)
        {
            return;
        }

        string first = picker.Matches[0];
        picker.Choose(first);
        e.Handled = true;
    }

    private void HandleOpenClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AssetPickerViewModel { Current: { Length: > 0 } path })
        {
            return;
        }

        OpenAssetEventArgs open = new(OpenRequestedEvent, path);
        RaiseEvent(open);
    }
}
