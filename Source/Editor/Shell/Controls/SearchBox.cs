using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace FlickerVoid.Editor.Controls;

/// <summary>
/// The editor's search field: a rounded text box with a magnifying glass, and a clear button once it holds text. What it searches
/// is whatever its <see cref="TextBox.Text"/> is bound to: a browser's search text, or a menu's.
/// </summary>
public sealed class SearchBox : TextBox
{
    private readonly Button _clear;

    public SearchBox()
    {
        Classes.Add("search");
        PlaceholderText = "Search";
        PathIcon glass = new() { Data = MaterialIcons.Search, Classes = { "searchGlass" } };
        InnerLeftContent = glass;
        PathIcon cross = new() { Data = MaterialIcons.Close };
        _clear = new Button { Content = cross, Classes = { "icon", "small", "searchClear" }, IsVisible = false, Focusable = false };
        _clear.Click += HandleClearClick;
        InnerRightContent = _clear;
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            _clear.IsVisible = !string.IsNullOrEmpty(Text);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !string.IsNullOrEmpty(Text))
        {
            Text = "";
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void HandleClearClick(object? sender, RoutedEventArgs e)
    {
        Text = "";
        Focus();
    }
}
