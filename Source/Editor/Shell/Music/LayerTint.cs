using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace FlickerVoid.Editor.Music;

/// <summary>The theme's colour for a layer by its place in the arrangement, which its lane, its header and its mixer strip share.</summary>
public static class LayerTint
{
    private const int Count = 6;

    public static FuncValueConverter<int, IBrush?> Brush { get; } = new(index => new ImmutableSolidColorBrush(Find(index)));

    public static Color Find(int index)
    {
        string key = $"EditorTrack{(Math.Abs(index) % Count) + 1}";
        if (Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out object? found) && found is Color color)
        {
            return color;
        }

        return Colors.Gray;
    }
}
