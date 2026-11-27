using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;

using FlickerVoid.Editor.Core.Music;

namespace FlickerVoid.Editor.Music;

/// <summary>The dock under the lanes with the mixer and the selected clip.</summary>
public partial class MusicDockView : UserControl
{
    public MusicDockView()
    {
        InitializeComponent();
    }

    private void HandleClose(object? sender, RoutedEventArgs args)
    {
        if (DataContext is MusicEditorViewModel editor)
        {
            editor.DockOpen = false;
        }
    }
}

/// <summary>True when the dock's tab number is the converter's parameter.</summary>
public sealed class DockTabConverter : IValueConverter
{
    public static DockTabConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is int tab && int.TryParse(parameter as string, out int wanted) && tab == wanted;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Makes a lit dot of a live row bright and an unlit one faint.</summary>
public sealed class LiveDotConverter : IValueConverter
{
    public static LiveDotConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        bool on = value is true;
        return on ? 1.0 : 0.25;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
